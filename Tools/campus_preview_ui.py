"""Campus-only viewer controls, separate from the blockout preview template."""
import re

STYLE = '''
*{box-sizing:border-box}body{margin:0;background:#d8e5e7;color:#252b2d;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;overflow:hidden}canvas{display:block;width:100vw;height:100vh;touch-action:none}
button{font:inherit;cursor:pointer}button:focus-visible,input:focus-visible{outline:2px solid #2a6b72;outline-offset:3px}
.top,.bottom,.hint,.number{display:none!important}.viewer-bar{position:absolute;left:16px;top:16px;height:38px;display:flex;align-items:center;gap:12px;padding:0 13px;background:#fff;border:1px solid #d8dcde;border-radius:4px;box-shadow:0 2px 8px #0000000c;font-size:13px;font-weight:600}.viewer-bar span{border-left:1px solid #d8dcde;padding-left:12px;color:#757b7e;font-size:11px;font-weight:400}
.tools-toggle{position:absolute;right:16px;top:16px;height:38px;padding:0 12px;border:1px solid #d8dcde;border-radius:4px;background:#fff;color:#30383b;font-size:12px}.tools-toggle[aria-expanded=true]{background:#eef1f2}
.panel{position:absolute;right:16px;top:64px;width:232px;background:#fff;border:1px solid #d8dcde;border-radius:4px;padding:14px;box-shadow:0 4px 16px #00000014;max-height:calc(100vh - 120px);overflow:auto}.panel[hidden]{display:none}.panel h2{font-size:12px;font-weight:600;margin:0 0 10px}.modes{display:grid;grid-template-columns:1fr 1fr;gap:2px}.modes button{border:0;border-radius:2px;background:#fff;color:#535d61;font-size:12px;text-align:left;padding:9px 7px}.modes button:hover{background:#f3f5f5}.modes button.active{background:#e8eff0;color:#235a61;font-weight:600;box-shadow:inset 2px 0 #235a61}
.field-title{font-size:11px;color:#727c80;margin:18px 0 8px}.floorbar{display:flex;border:1px solid #dce1e2;border-radius:3px;overflow:hidden}.floorbar button{border:0;border-right:1px solid #dce1e2;flex:1;padding:7px 0;background:#fff;color:#596266;font-size:11px}.floorbar button:last-child{border:0}.floorbar button.active{background:#edf2f3;color:#235a61;font-weight:600}
.line{border-top:1px solid #e5e8e9;margin:14px 0}.check{display:flex;align-items:center;justify-content:space-between;gap:12px;cursor:pointer;font-size:12px;color:#4e595d;padding:6px 0}.check input{order:2;margin:0;width:14px;height:14px;accent-color:#39656b}.info{font-size:11px;line-height:1.65;color:#7b8589;margin:0}.legend{display:none}.viewtag{position:absolute;left:16px;bottom:16px;color:#414a4f;background:#fff;border:1px solid #d8dcde;border-radius:3px;padding:8px 11px;font-size:11px}.gesture{position:absolute;right:16px;bottom:16px;padding:8px 11px;background:#fff;border:1px solid #d8dcde;border-radius:3px;color:#7b8589;font-size:10px;pointer-events:none}.error{position:absolute;left:16px;bottom:60px;background:#fff4ed;padding:15px;font-size:13px;max-width:400px}
@media(max-width:600px){.viewer-bar{left:12px;top:12px}.viewer-bar span{display:none}.tools-toggle{right:12px;top:12px}.panel{right:12px;top:58px;width:218px}.viewtag{left:12px;bottom:12px}.gesture{display:none}}
'''

CONTROLS = '''
<div class="viewer-bar">校园疏散演练<span>场景预览</span></div>
<button class="tools-toggle" id="toolsToggle" aria-expanded="true" aria-controls="viewerTools">视角与显示</button>
<aside class="panel" id="viewerTools" aria-label="场景控制">
<h2>视角</h2><div class="modes">
<button data-view="exterior">建筑总览</button><button data-view="floor">楼层平面</button>
<button data-view="lift">电梯厅</button><button data-view="stair">楼梯间</button></div>
<div class="field-title">楼层</div><div class="floorbar"><button data-floor="1">1F</button><button data-floor="2">2F</button><button data-floor="3">3F</button><button class="active" data-floor="4">4F</button></div>
<div class="line"></div>
<label class="check"><input id="cutaway" type="checkbox">屋顶剖切</label>
<label class="check"><input id="routes" type="checkbox">疏散路线</label>
<label class="check"><input id="sunlight" type="checkbox" checked>太阳阴影</label>
<label class="check"><input id="atmosphere" type="checkbox">火警烟雾</label>
<div class="line"></div><p class="info" id="info"></p></aside>
<div class="viewtag" id="viewtag"></div><div class="gesture">拖动旋转 · 滚轮缩放 · 双击复位</div>
'''


def restyle_preview(template):
    template = re.sub(r'<title>.*?</title>', '<title>校园疏散演练 · 场景预览</title>', template, count=1)
    template = re.sub(r'<span class="number">\d+</span>', '', template)
    template = re.sub(r'<style>.*?</style>', '<style>' + STYLE + '</style>', template, count=1, flags=re.S)
    template = re.sub(r'<div class="top">.*?(?=<div id="labels">)', lambda _: CONTROLS, template, count=1, flags=re.S)
    template = re.sub(r"document.querySelector\('\.top'\)\.style.display=.*?document.querySelector\('\.bottom'\)\.style.display=.*?;", '', template)
    template = template.replace('四楼教室</button>', '401 教室</button>').replace('四楼走廊</button>', '走廊</button>')
    template = template.replace('校园公告栏</button>', '公告栏</button>').replace('外窗与隔墙</button>', '外窗</button>')
    template = re.sub(r"(exterior|floor|lift|stair|classroom|corridor|notice|window):'\d\d / ", r"\1:'", template)
    template = template.replace('`02 / ${floor}F 楼层布局`', '`${floor}F · 楼层平面`')
    template = template.replace("document.getElementById('info').textContent=descriptions[value];", "document.getElementById('info').textContent=({exterior:'教学楼与室外集合点。',floor:'选择楼层查看平面与疏散路线。',lift:'火警时使用楼梯疏散。',stair:'楼梯通往一楼侧出口。',classroom:'401 · 教室',corridor:'4F · 教学楼走廊',notice:'4F · 学生公告栏',window:'教室外窗'})[value];")
    anchor = "window.addEventListener('resize',()=>dirty=true);"
    script = """
const toolsToggle=document.getElementById('toolsToggle'),viewerTools=document.getElementById('viewerTools');
function showTools(open){viewerTools.hidden=!open;toolsToggle.setAttribute('aria-expanded',String(open));}
showTools(!matchMedia('(max-width:600px)').matches);
toolsToggle.addEventListener('click',()=>showTools(viewerTools.hidden));
document.addEventListener('keydown',e=>{if(e.key==='Escape')showTools(false)});
document.querySelectorAll('[data-view],[data-floor]').forEach(b=>b.addEventListener('click',()=>{if(matchMedia('(max-width:600px)').matches)showTools(false)}));
"""
    assert anchor in template
    return template.replace(anchor, script + anchor, 1)
