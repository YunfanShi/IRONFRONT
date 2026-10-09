import * as THREE from 'three';
export type EquipmentItem = 'repair' | 'medkit' | 'ammo' | 'beacon' | 'frag' | 'smoke' | 'at' | 'aa' | 'mountedMG' | 'cockpit';
/** Original, separate geometry for every usable item. No pistol fallback. */
export class EquipmentView {
    readonly root = new THREE.Group();
    private models = new Map<EquipmentItem, THREE.Group>();
    private mats: THREE.Material[] = [];
    private current:EquipmentItem|null=null;
    constructor(camera: THREE.Camera) {
        camera.add(this.root);
        this.root.position.set(.24, -.23, -.55);
        this.root.scale.setScalar(.65);
        this.root.renderOrder = 8;
        const mat = (color: number) => { const m = new THREE.MeshStandardMaterial({ color, roughness: .66, metalness: .32, depthTest: false, depthWrite: false }); this.mats.push(m); return m; };
        const steel = mat(0x424d52), black = mat(0x161d20), olive = mat(0x576049), red = mat(0xb25140), white = mat(0xdbd6bd), glass = mat(0x588998);
        for (const id of ['repair', 'medkit', 'ammo', 'beacon', 'frag', 'smoke', 'at', 'aa', 'mountedMG', 'cockpit'] as EquipmentItem[]) {
            const g = new THREE.Group();
            g.visible = false;
            this.models.set(id, g);
            this.root.add(g);
            const box = (w: number, h: number, d: number, m: THREE.Material, x = 0, y = 0, z = 0) => { const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), m); mesh.position.set(x, y, z); g.add(mesh); return mesh; };
            const tube = (r: number, l: number, m: THREE.Material, x = 0, y = 0, z = 0) => { const mesh = new THREE.Mesh(new THREE.CylinderGeometry(r, r, l, 16), m); mesh.rotation.x = Math.PI / 2; mesh.position.set(x, y, z); g.add(mesh); return mesh; };
            if(id!=='cockpit'){box(.085,.11,.15,black,.08,-.17,.07);box(.11,.16,.22,olive,.08,-.27,.17);box(.085,.11,.15,black,-.13,-.1,-.23);box(.11,.15,.2,olive,-.15,-.2,-.12);}
            if (id === 'at' || id === 'aa') {
                tube(id === 'aa' ? .09 : .12, 1.05, olive, 0, 0, -.25);
                tube(.135, .09, black, 0, 0, -.76);
                tube(.14, .14, steel, 0, 0, .27);
                box(.08, .22, .13, black, .02, -.15, .04);
                box(.06, .09, .16, steel, -.12, .06, -.06);
                if (id === 'aa') {
                    box(.2, .12, .26, glass, .13, .1, -.15);
                    box(.055, .19, .03, black, .16, .22, -.2);
                }
                else
                    box(.04, .23, .03, steel, 0, .16, -.4);
            }
            else if (id === 'mountedMG') {
                box(.16, .2, .39, steel, 0, 0, -.05);
                tube(.03, .8, black, 0, .01, -.58);
                tube(.05, .32, steel, 0, .01, -.33);
                box(.22, .23, .26, olive, -.18, -.05, .03);
                box(.08, .06, .14, black, 0, .14, -.07);
                box(.07, .07, .018, glass, 0, .19, -.12);
                for (let i = 0; i < 6; i++)
                    tube(.015, .065, white, -.15 - i * .025, .06, -.04);
                box(.11, .18, .12, black, 0, -.16, .19);
                box(.7, .035, .03, steel, 0, -.26, -.24);
            }
            else if (id === 'repair') {
                box(.12, .15, .23, red, 0, 0, .02);
                tube(.024, .33, steel, 0, .06, -.22);
                box(.055, .17, .065, black, 0, -.13, .02);
                tube(.02, .22, black, .12, -.08, .03);
            }
            else if (id === 'frag') {
                const shell = new THREE.Mesh(new THREE.SphereGeometry(.1, 12, 10), olive);
                shell.scale.y = 1.35;
                g.add(shell);
                box(.03, .035, .2, steel, .05, .12, -.025);
                tube(.025, .03, black, 0, .13, 0);
                const ring = new THREE.Mesh(new THREE.TorusGeometry(.035, .009, 6, 12), steel);
                ring.position.set(-.06, .15, .02);
                g.add(ring);
            }
            else if (id === 'smoke') {
                tube(.075, .24, white).rotation.x = 0;
                box(.035, .035, .12, steel, .055, .15, 0);
                tube(.081, .04, olive, 0, .04, 0);
                box(.115, .07, .005, red, 0, 0, .077);
            }
            else if (id === 'cockpit') {
                box(1.65, .24, .4, black, 0, -.28, -.16);
                box(.32, .16, .035, glass, -.35, -.14, -.38);
                box(.25, .13, .03, glass, .22, -.13, -.38);
                box(.045, 1.25, .09, steel, -.9, .18, -.5);
                box(.045, 1.25, .09, steel, .65, .18, -.5);
                for (let i = 0; i < 5; i++)
                    box(.035, .035, .03, i % 2 ? red : white, -.4 + i * .09, -.27, -.38);
            }
            else {
                box(.32, .22, .23, id === 'medkit' ? olive : steel);
                box(.23, .03, .05, black, 0, .14, 0);
                if (id === 'medkit') {
                    box(.13, .035, .007, red, 0, 0, .119);
                    box(.035, .13, .007, red, 0, 0, .12);
                }
                if (id === 'ammo')
                    for (let i = 0; i < 5; i++)
                        tube(.013, .11, white, -.09 + i * .043, .07, -.01);
                if (id === 'beacon') {
                    box(.1, .05, .08, glass, 0, .07, .12);
                    tube(.007, .46, black, .12, .31, 0).rotation.x = 0;
                }
            }
        }
    }
    muzzleWorldPosition(){this.root.updateWorldMatrix(true,true);const p=this.root.localToWorld(new THREE.Vector3(0,this.current==='mountedMG'?.01:0,this.current==='mountedMG'?-.98:-.805));return {x:p.x,y:p.y,z:p.z};}
    render(item: EquipmentItem | null, age: number, ads = false) { this.current=item;this.root.visible = item !== null; for (const [id, g] of this.models)
        g.visible = id === item; const action = Math.sin(Math.min(1, Math.max(0, age)) * Math.PI); this.root.position.set(item === 'cockpit' ? 0 : ads && (item === 'mountedMG'||item === 'at'||item === 'aa') ? 0 : .24, item === 'cockpit' ? -.35 : -.23 - action * .04, ads && (item === 'mountedMG'||item === 'at'||item === 'aa') ? -.4 : -.55); this.root.rotation.set(action * .16, 0, action * -.07); }
    dispose() { this.root.removeFromParent(); this.root.traverse(o => { if (o instanceof THREE.Mesh)
        o.geometry.dispose(); }); for (const m of this.mats)
        m.dispose(); }
}
