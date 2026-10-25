using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeThrowableId { Frag, Smoke }

    // Matches the selectable slots in main/src/combat/Weapons.ts. The class
    // determines the gadget; invalid saved values are repaired on load.
    [Serializable]
    public sealed class PrototypeLoadout
    {
        private const string SaveKey = "ironfront.unity.loadout";
        public PrototypeInfantryClass role = PrototypeInfantryClass.Assault;
        public PrototypeWeaponId primary = PrototypeWeaponId.Carbine;
        public PrototypeWeaponId secondary = PrototypeWeaponId.Pistol;
        public PrototypeThrowableId throwable = PrototypeThrowableId.Frag;

        public PrototypeLoadout Copy() => new PrototypeLoadout
        {
            role = role, primary = primary, secondary = secondary,
            throwable = throwable
        };

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(PrototypeInfantryClass), role))
                role = PrototypeInfantryClass.Assault;
            if (!PrototypeInfantryRoles.IsPrimaryWeaponAllowed(role, primary))
                primary = PrototypeInfantryRoles.DefaultWeapon(role);
            if (!PrototypeInfantryRoles.IsSecondaryWeaponAllowed(secondary))
                secondary = PrototypeWeaponId.Pistol;
            if (!Enum.IsDefined(typeof(PrototypeThrowableId), throwable))
                throwable = PrototypeThrowableId.Frag;
        }

        public static PrototypeLoadout Load()
        {
            PrototypeLoadout saved = null;
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, "");
                if (!string.IsNullOrEmpty(json))
                    saved = JsonUtility.FromJson<PrototypeLoadout>(json);
            }
            catch (Exception) { /* Invalid local settings use the default kit. */ }
            if (saved == null) saved = new PrototypeLoadout();
            saved.Validate();
            return saved;
        }

        public void Save()
        {
            Validate();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
