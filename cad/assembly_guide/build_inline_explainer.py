from pathlib import Path
import json

root=Path(__file__).resolve().parents[2]
folder=root/'cad/assembly_guide'
template=(folder/'robot-assembly-explainer.template.html').read_text(encoding='utf-8')
scene=json.loads((folder/'native_scene.json').read_text(encoding='utf-8'))
bom=json.loads((root/'design/procurement_20261028.json').read_text(encoding='utf-8'))
ex=json.loads((root/'design/assembly_item_explanations_20261028.json').read_text(encoding='utf-8'))
assert len(bom['items'])==len(ex)==87
guide={'items':bom['items'],'explanations':ex}
fragment=template.replace('__NATIVE_JSON__',json.dumps(scene,ensure_ascii=False,separators=(',',':')).replace('</','<\\/'))
fragment=fragment.replace('__GUIDE_JSON__',json.dumps(guide,ensure_ascii=False,separators=(',',':')).replace('</','<\\/'))
target=Path('C:/Users/Lenovo/.codex/visualizations/2026/10/11/01a12b65-609a-7bf2-be3d-549e726e3b11/robot-assembly-explainer.html')
target.parent.mkdir(parents=True,exist_ok=True)
target.write_text(fragment,encoding='utf-8')
assert target.stat().st_size<1_000_000
assert '<html' not in fragment and '<body' not in fragment
assert '__NATIVE_JSON__' not in fragment and '__GUIDE_JSON__' not in fragment
print('FRAGMENT='+str(target));print('FRAGMENT_BYTES='+str(target.stat().st_size))
