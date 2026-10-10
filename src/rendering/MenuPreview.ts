import * as THREE from 'three';
import { buildWeaponModel } from './WeaponModels';
import type { Loadout } from '../combat/Weapons';
/** In-engine operator and selected weapon, rendered on demand to avoid an idle second game loop. */
export class MenuPreview {
    private renderer: THREE.WebGLRenderer;
    private scene = new THREE.Scene();
    private camera = new THREE.PerspectiveCamera(35, 1, .1, 30);
    private body=new THREE.Group();private frame=0;private lastFrame=0;private stopped=false;
    private resize=()=>this.draw();
    private visibility=()=>{if(document.hidden){cancelAnimationFrame(this.frame);this.frame=0;}else if(!this.stopped&&!this.frame)this.frame=requestAnimationFrame(this.animate);};
    private animate=(now:number)=>{if(this.stopped||document.hidden){this.frame=0;return;}if(now-this.lastFrame>=1000/24){this.lastFrame=now;this.body.rotation.y=-.25+Math.sin(now*.00025)*.035;this.draw();}this.frame=requestAnimationFrame(this.animate);};
    private mats: THREE.MeshStandardMaterial[] = [];
    constructor(private canvas: HTMLCanvasElement, loadout: Loadout, lobbySide=false) {
        this.renderer = new THREE.WebGLRenderer({ canvas, alpha: false, antialias: true });
        this.renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.scene.add(new THREE.HemisphereLight(0xd9ebe7, 0x30392b, 3));
        const key = new THREE.DirectionalLight(0xffe2af, 3);
        key.position.set(3, 5, 5);
        this.scene.add(key);
        this.camera.position.set(3.1, 2.4, 5.5);
        this.camera.lookAt(0, 1.25, 0);
        const mat = (color: number) => { const m = new THREE.MeshStandardMaterial({ color, roughness: .8 }); this.mats.push(m); return m; }, cloth = mat(loadout.classId === 'recon' ? 0x667360 : 0x71775d), dark = mat(0x232e2d), skin = mat(0xa48a70), metal = mat(0x414849), tan = mat(0x9b906f), glass = mat(0x182e34);
        const body = this.body;
        body.rotation.y = -.25;
        this.scene.add(body);
        this.scene.background=new THREE.Color(0x0a1118);this.scene.fog=new THREE.Fog(0x0a1118,8,24);
        const stage=new THREE.Group();this.scene.add(stage);const stageMat=mat(0x1e2b33),floor=mat(0x171f24);
        const panel=(w:number,h:number,d:number,x:number,y:number,z:number,m:THREE.Material)=>{const mesh=new THREE.Mesh(new THREE.BoxGeometry(w,h,d),m);mesh.position.set(x,y,z);stage.add(mesh);};
        panel(20,.12,16,0,-.06,0,floor);panel(20,8,.2,0,4,-4,stageMat);
        for(let i=-4;i<=4;i++){panel(.1,7,.22,i*2,3.5,-3.8,floor);panel(1.5,.025,.04,i*2,.1,-2,mat(0x758c92));}
        for(const x of [-4,4]){panel(.13,.13,9,x,4,0,stageMat);const lamp=new THREE.PointLight(x<0?0x5aadc7:0xd3aa7b,12,12);lamp.position.set(x,3,1);stage.add(lamp);}
        for(const x of [-2.6,2.3]){panel(1.1,.8,.85,x,.4,-1.7,mat(0x35413e));panel(1.12,.09,.89,x,.83,-1.7,stageMat);}
        // Frame the operator in the clear right-hand space of the lobby.
        body.position.x=1.2;this.camera.position.set(4.1,2.5,6.8);this.camera.lookAt(lobbySide?-1.1:.45,1.25,0);
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
        box(.55, .55, .55, skin, 0, 1.99, 0);
        box(.57,.08,.57,dark,0,2.285,0);box(.32,.1,.02,dark,0,1.80,.282);

        box(.10,.06,.015,glass,-.14,2.04,.282);
        box(.10,.06,.015,glass,.14,2.04,.282);
        box(.035, .12, .03, tan, 0, 2.17, .17);
        box(.07, .1, .04, loadout.classId === 'medic' ? mat(0xcb6955) : mat(0x90bfd0), .33, 1.5, .2);
        const weapon = buildWeaponModel(loadout.primary, { steel: metal, matte: dark, grip: dark, tan, glove: dark, gold: tan, glass });
        weapon.scale.setScalar(.6);
        weapon.rotation.set(.12, -Math.PI / 2, -.1);
        weapon.position.set(.2, 1.14, .48);
        body.add(weapon);
        this.draw();window.addEventListener('resize',this.resize);document.addEventListener('visibilitychange',this.visibility);this.frame=requestAnimationFrame(this.animate);
    }
    draw() { const width = Math.max(1, this.canvas.clientWidth), height = Math.max(1, this.canvas.clientHeight); if(this.canvas.width!==Math.floor(width*this.renderer.getPixelRatio())||this.canvas.height!==Math.floor(height*this.renderer.getPixelRatio()))this.renderer.setSize(width, height, false); this.camera.aspect = width / height; this.camera.updateProjectionMatrix(); this.renderer.render(this.scene, this.camera); }
    dispose() { this.stopped=true;cancelAnimationFrame(this.frame);this.frame=0;window.removeEventListener('resize',this.resize);document.removeEventListener('visibilitychange',this.visibility);this.scene.traverse(o => { if (o instanceof THREE.Mesh)
        o.geometry.dispose(); }); for (const m of this.mats)
        m.dispose(); this.renderer.dispose(); }
}
