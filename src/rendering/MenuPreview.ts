import * as THREE from 'three';
import { buildWeaponModel } from './WeaponModels';
import type { Loadout } from '../combat/Weapons';
/** In-engine operator and selected weapon, rendered on demand to avoid an idle second game loop. */
export class MenuPreview {
    private renderer: THREE.WebGLRenderer;
    private scene = new THREE.Scene();
    private camera = new THREE.PerspectiveCamera(35, 1, .1, 30);
    private mats: THREE.MeshStandardMaterial[] = [];
    constructor(private canvas: HTMLCanvasElement, loadout: Loadout) {
        this.renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
        this.renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.scene.add(new THREE.HemisphereLight(0xd9ebe7, 0x30392b, 3));
        const key = new THREE.DirectionalLight(0xffe2af, 3);
        key.position.set(3, 5, 5);
        this.scene.add(key);
        this.camera.position.set(3.1, 2.4, 5.5);
        this.camera.lookAt(0, 1.25, 0);
        const mat = (color: number) => { const m = new THREE.MeshStandardMaterial({ color, roughness: .8 }); this.mats.push(m); return m; }, cloth = mat(loadout.classId === 'recon' ? 0x667360 : 0x71775d), dark = mat(0x232e2d), skin = mat(0xa48a70), metal = mat(0x414849), tan = mat(0x9b906f), glass = mat(0x182e34);
        const body = new THREE.Group();
        body.rotation.y = -.25;
        this.scene.add(body);
        const box = (w: number, h: number, d: number, m: THREE.Material, x: number, y: number, z: number) => { const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), m); mesh.position.set(x, y, z); body.add(mesh); return mesh; };
        box(.64, .72, .36, cloth, 0, 1.3, 0);
        box(.54, .5, .16, dark, 0, 1.35, .24);
        for (const x of [-.17, 0, .17])
            box(.14, .2, .12, tan, x, 1.23, .35);
        box(.51, .11, .42, dark, 0, .9, 0);
        box(.45, .5, .28, dark, 0, 1.4, -.25);
        for (const x of [-.19, .19]) {
            box(.24, .44, .29, cloth, x, .65, 0);
            box(.25, .13, .3, dark, x, .42, .05);
            box(.23, .37, .24, cloth, x, .23, 0);
            box(.25, .17, .43, dark, x, .09, .08);
            const arm = box(.2, .54, .22, cloth, x * 2.1, 1.31, .12);
            arm.rotation.z = x < 0 ? -.28 : .35;
            box(.15, .16, .18, dark, x * 1.3, 1.04, .38);
        }
        box(.42, .42, .38, skin, 0, 1.93, 0);
        box(.3, .13, .27, dark, 0, 1.84, .025);
        box(.45, .12, .42, cloth, 0, 2.1, 0);
        box(.07, .045, .02, glass, -.11, 1.98, .20);
        box(.07, .045, .02, glass, .11, 1.98, .20);
        box(.035, .12, .03, tan, 0, 2.17, .17);
        box(.07, .1, .04, loadout.classId === 'medic' ? mat(0xcb6955) : mat(0x90bfd0), .33, 1.5, .2);
        const weapon = buildWeaponModel(loadout.primary, { steel: metal, matte: dark, grip: dark, tan, glove: dark, gold: tan, glass });
        weapon.scale.setScalar(.6);
        weapon.rotation.set(.12, -Math.PI / 2, -.1);
        weapon.position.set(.2, 1.14, .48);
        body.add(weapon);
        this.draw();
    }
    draw() { const width = Math.max(1, this.canvas.clientWidth), height = Math.max(1, this.canvas.clientHeight); this.renderer.setSize(width, height, false); this.camera.aspect = width / height; this.camera.updateProjectionMatrix(); this.renderer.render(this.scene, this.camera); }
    dispose() { this.scene.traverse(o => { if (o instanceof THREE.Mesh)
        o.geometry.dispose(); }); for (const m of this.mats)
        m.dispose(); this.renderer.dispose(); }
}
