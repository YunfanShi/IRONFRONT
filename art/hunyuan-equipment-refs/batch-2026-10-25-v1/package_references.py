# -*- coding: utf-8 -*-
from pathlib import Path
import json, shutil, struct, hashlib, zipfile, zlib, html

root = Path(__file__).resolve().parent
records = json.loads((root / "generation-records.json").read_text(encoding="utf-8"))
briefs = json.loads((root / "asset-briefs.json").read_text(encoding="utf-8"))["assets"]
assets = {a["id"]: a for a in briefs}
gun_ids = ["m89", "vx9", "h60", "br44", "s12", "p8"]
prop_ids = ["ammo", "medkit", "repair", "beacon", "frag", "smoke"]
view_names = {"left":"左视图", "right":"右视图", "front":"前视图", "back":"后视图"}
issues = {
 "m89":"右视图多出球形柄，与左视图的外观设定不一致；修图被工具审核拦截，仍待修正。",
 "h60":"前视图握把过长、后视图枪托过高，底部轮廓与侧视图不一致；修图被工具审核拦截，仍待修正。",
 "s12":"左右两侧出现重复球形柄，前视图有俯视透视，后视图多出支架且柄的左右位置不一致；修图被工具审核拦截，仍待修正。",
 "at":"仅两张侧视图。前视图角度错误未采用，后视图被工具审核拦截。",
 "aa":"图像生成被工具审核拦截，未完成。"
}
files = []
for o in records["selected"]:
    target = root / o["id"] / (o["id"] + "-" + o["view"] + ".png")
    target.parent.mkdir(exist_ok=True)
    shutil.copy2(o["source"], target)
    data = target.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    width, height = struct.unpack(">II", data[16:24])
    offset = 8
    found_end = False
    while offset < len(data):
        size = struct.unpack(">I", data[offset:offset+4])[0]
        chunk = data[offset+4:offset+8+size]
        expected = struct.unpack(">I", data[offset+8+size:offset+12+size])[0]
        assert zlib.crc32(chunk) & 0xffffffff == expected, target
        offset += size + 12
        if chunk[:4] == b"IEND":
            found_end = True
            break
    assert found_end
    files.append({"asset":o["id"],"name":assets[o["id"]]["name"],"view":o["view"],
                  "file":target.relative_to(root).as_posix(),"width":width,"height":height,
                  "bytes":len(data),"sha256":hashlib.sha256(data).hexdigest(),
                  "review_note":issues.get(o["id"],"概念参考；未经同一三维模型投影校验。")})
assert len(files) == 50
for aid in gun_ids + prop_ids:
    assert {f["view"] for f in files if f["asset"] == aid} == set(view_names)
manifest = {"version":"2026-10-25-v1","purpose":"混元图生三维的外观概念参考",
            "mode":"内置 image_gen；初次生成 + 参考图编辑；没有三维模型生成或游戏接入",
            "complete_view_sets":12,"four_view_images":48,"extra_side_images":2,
            "quality":"概念参考草案；M89、H60、S12 存在已知跨视图差异。",
            "known_issues":issues,"images":files}
(root/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")

readme = """IRON FRONT / 武器与道具参考图 / 2026-10-25 v1

本批交付是供混元图生三维使用的外观参考图片，不是已经制作或接入游戏的三维模型。

内容
• 6 把枪：M89、VX-9、H60、BR-44、S12、P8，每把左、右、前、后各一张，共 24 张。
• 6 类道具：弹药补给包、医疗急救包、工程维修工具、复活信标、破片手雷、烟雾弹，每类四张，共 24 张。
• 反装甲发射器：另附左、右两张侧视参考；四视图未完成。
• 防空发射器：未生成。工具审核连续拒绝了生成请求。
• 共 50 张独立 PNG，其中 48 张组成 12 套四视参考。
• 已有 IF-27 参考与旧模型未改动；这份包不重复包含 IF-27。

文件使用
打开 index.html 可浏览全部图片。每个物品有独立文件夹：
left = 左；right = 右；front = 前；back = 后。
枪械“前”指枪口端，“后”指枪托/握把后端。
包类“前”指扣具或标识一面，“后”指背带一面。
图片为白底独立原图，无拼版；根据混元界面对应方向逐张选择同一物品的图片。
不要把不同物品的参考混入同一次生成。

已知问题（交付前已审查，但本轮未能全部修正）
1. M89 右视图出现多余球形柄，左右外观不一致。
2. H60 前视图握把过长、后视图枪托过高，与侧面底部轮廓不一致。
3. S12 左右重复球形柄；前视有俯视透视；后视多出支架且柄的左右位置不一致。
以上三项的最后修图请求被图像工具审核拦截；相关图仍以“待修正参考”交付。
4. 反装甲发射器前视图角度错误，已排除；后视图被审核拦截，未交付。
5. 防空发射器未生成，未以其他图代替。

用途边界
本批由 AI 分别生成各方向，并非同一三维模型的严格正交投影。
镜头、占画幅大小、细小结构及遮挡可能不一致；所有图都是概念参考，不能视为几何验收通过。
建模时先统一侧面轮廓和外观设计，再校对其他方向；M89、H60、S12 应先解决上述差异。
生成后的模型仍需检查比例、拓扑、材质、手部握持和开镜位置，并拆分需要动画的部件。

生成与记录
工具：内置 image_gen。
模式：首张生成，其余方向以图片为参考生成；投掷物左右视图做了参考图编辑。
asset-briefs.json：物品设计与首图提示词。
view-jobs.json：视图提示词。
generation-records.json：实际成功生成、选图记录及未成功的最后修图提示词。
manifest.json：选定图片、分辨率、SHA-256 和审查说明。
输出原图保留在本机生成目录；现有模型与旧版本保留。
"""
(root/"README.txt").write_text(readme,encoding="utf-8")

sections=[]
for aid in gun_ids + prop_ids + ["at"]:
    a=assets[aid]
    tag="四视参考 · 待修正" if aid in ["m89","h60","s12"] else ("侧视参考 · 不完整" if aid=="at" else "四视概念参考")
    warning=issues.get(aid,"")
    cards=[]
    for view in ["left","right","front","back"]:
        match=next((f for f in files if f["asset"]==aid and f["view"]==view),None)
        if match:
            path=html.escape(match["file"])
            cards.append(f'<figure><a href="{path}" target="_blank"><img loading="lazy" src="{path}" alt="{html.escape(a["name"])} {view_names[view]}"></a><figcaption>{view_names[view]} <span>{match["width"]} × {match["height"]}</span></figcaption></figure>')
        else:
            cards.append(f'<figure class="missing"><div>未交付</div><figcaption>{view_names[view]}</figcaption></figure>')
    sections.append(f'<section id="{aid}" data-kind="{"gun" if aid in gun_ids else "prop"}"><div class="section-title"><h2>{html.escape(a["name"])}</h2><span class="badge">{tag}</span></div>{f"<p class=warning>{html.escape(warning)}</p>" if warning else ""}<div class="grid">{"".join(cards)}</div></section>')
doc="""<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>IRON FRONT · 枪械与道具参考图</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#10171c;color:#edf1f2;font:15px/1.65 system-ui,-apple-system,"PingFang SC",sans-serif}main{max-width:1500px;margin:auto;padding:48px 28px}header{border-bottom:1px solid #34434c;padding-bottom:28px}.eyebrow{font-size:12px;letter-spacing:.18em;color:#a7bcac}h1{font-size:38px;line-height:1.25;margin:12px 0}header p{max-width:900px;color:#b9c6cd}a{color:#b8dbc3}nav{display:flex;gap:10px;flex-wrap:wrap;margin-top:22px}nav a{border:1px solid #41584a;padding:7px 14px;border-radius:6px;text-decoration:none}section{margin-top:40px;padding-top:12px}.section-title{display:flex;align-items:center;gap:20px;flex-wrap:wrap}h2{font-size:23px;margin:0}.badge{color:#aebdc4;font-size:12px;border:1px solid #43525c;border-radius:20px;padding:3px 12px}.warning{color:#efcb8c;background:#302a20;padding:10px 14px;border-radius:6px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;margin-top:16px}figure{margin:0;overflow:hidden;background:#fff;border-radius:8px}figure img{display:block;width:100%;aspect-ratio:3/2;object-fit:contain}figcaption{padding:10px 14px;background:#202b33;color:#edf1f2}figcaption span{float:right;color:#8ea2b0;font-size:12px}.missing>div{aspect-ratio:3/2;display:grid;place-items:center;background:#202830;color:#81909a}footer{border-top:1px solid #34434c;margin-top:50px;padding-top:20px;color:#9cb0bd}@media(max-width:650px){main{padding:25px 15px}.grid{grid-template-columns:1fr}h1{font-size:28px}}
</style><main><header><div class="eyebrow">IRON FRONT / HUNYUAN REFERENCES / V1</div><h1>枪械与道具 · 四视参考</h1><p>6 把枪 + 6 类道具，48 张独立四视参考；另附反装甲发射器 2 张侧视参考。点击图片可查看原图。</p><p>这是 AI 外观概念参考，尚未生成或接入三维模型。M89、H60、S12 有待修正差异，详见各组说明。防空发射器生成被工具审核拦截，未交付。</p><nav><a href="#m89">枪械</a><a href="#ammo">补给与工具</a><a href="#frag">投掷物</a><a href="#at">发射器侧视</a><a href="README.txt">完整说明</a><a href="manifest.json">图片清单</a></nav></header>"""+"".join(sections)+"""<footer>内置 image_gen 生成 / 首图 + 参考图编辑。提示词与记录随包附带。所有文件可离线查看。</footer></main></html>"""
(root/"index.html").write_text(doc,encoding="utf-8")

meta = ["README.txt","manifest.json","asset-briefs.json","view-jobs.json","generation-records.json"]
bundle_specs = [
 ("ironfront-weapons-6-four-views-v1.zip", [f for f in files if f["asset"] in gun_ids], False),
 ("ironfront-props-6-four-views-v1.zip", [f for f in files if f["asset"] in prop_ids], False),
 ("ironfront-equipment-reference-pack-v1.zip", files, True)
]
bundles=[]
for name, subset, include_gallery in bundle_specs:
    output = root.parent/name
    with zipfile.ZipFile(output,"w",zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for f in subset:
            z.write(root/f["file"],f["file"])
        for m in meta:
            if not include_gallery and m == "manifest.json":
                sub_manifest = dict(manifest)
                sub_manifest["images"] = subset
                sub_manifest["complete_view_sets"] = len(subset) // 4
                sub_manifest["four_view_images"] = len(subset)
                sub_manifest["extra_side_images"] = 0
                sub_manifest["archive_scope"] = name
                z.writestr(m, json.dumps(sub_manifest, ensure_ascii=False, indent=2))
            elif not include_gallery and m == "README.txt":
                scope = "本分包只包含 " + str(len(subset)) + " 张 PNG。下方内容说明与生成记录描述整个批次；图片清单 manifest.json 仅列本分包文件。完整图册 index.html 位于总包。\\n\\n"
                z.writestr(m, scope.replace("\\n", "\n") + readme)
            else:
                z.write(root/m,m)
        if include_gallery:
            z.write(root/"index.html","index.html")
    with zipfile.ZipFile(output) as z:
        assert z.testzip() is None
        assert sum(n.endswith(".png") for n in z.namelist()) == len(subset)
    bundles.append({"path":str(output),"png_count":len(subset),"bytes":output.stat().st_size})
print(json.dumps({"files":len(files),"complete_sets":12,"dimensions":sorted(set((f["width"],f["height"]) for f in files)),"bundles":bundles,"gallery":str(root/"index.html")},ensure_ascii=False,indent=2))

