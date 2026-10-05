"""Add cached directional shadows to the standalone Campus WebGL preview.

The browser uses approximate lighting. Unity captures remain the PBR reference.
"""
import re

FRAGMENT = r'''#version 300 es
precision highp float;
precision highp sampler2DArray;
in vec3 normal; in vec4 tint; in vec3 worldPos; flat in int layer;
uniform sampler2DArray surfaces;
uniform float tileMeters[64];
uniform sampler2D sunDepth; uniform mat4 sunVP; uniform bool sunShadows;
uniform vec3 cameraEye; uniform float gloss[64]; uniform float metal[64];
out vec4 outputColor;
float visibility(vec3 norm, vec3 lightDir) {
    if(!sunShadows) return 1.;
    vec4 position=sunVP*vec4(worldPos,1.);
    vec3 p=position.xyz/position.w*.5+.5;
    if(any(lessThan(p,vec3(0.)))||any(greaterThan(p,vec3(1.)))) return 1.;
    float bias=max(.00008,.0003*(1.-dot(norm,lightDir))), sum=0.;
    for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++)
        sum+=p.z-bias<=texture(sunDepth,p.xy+vec2(float(x),float(y))/2048.).r?1.:0.;
    return sum/9.;
}
void main(){
    vec3 norm=normalize(normal),weights=pow(abs(norm),vec3(4.));
    weights/=max(weights.x+weights.y+weights.z,.0001);
    vec3 uv=worldPos/max(tileMeters[layer],.01);
    vec3 tex=texture(surfaces,vec3(uv.zy,float(layer))).rgb*weights.x
        +texture(surfaces,vec3(uv.xz,float(layer))).rgb*weights.y
        +texture(surfaces,vec3(uv.xy,float(layer))).rgb*weights.z;
    vec3 base=pow(max(tint.rgb*tex,vec3(0.)),vec3(2.2));
    vec3 lightDir=normalize(vec3(.4,.85,.5)), viewDir=normalize(cameraEye-worldPos);
    float shadow=visibility(norm,lightDir), ndl=max(dot(norm,lightDir),0.);
    float inside=step(abs(worldPos.x),17.8)*step(abs(worldPos.z),8.79);
    float ambient=.25+.08*max(norm.y,0.)+.18*inside;
    vec3 reflected=reflect(-lightDir,norm);
    float shine=pow(max(dot(reflected,viewDir),0.),mix(12.,110.,gloss[layer]));
    vec3 spec=mix(vec3(.04),base,metal[layer])*shine*gloss[layer]*4.*shadow;
    vec3 lit=base*(ambient+vec3(1.,.96,.89)*ndl*shadow*.9)+spec;
    outputColor=vec4(pow(lit,vec3(1./2.2)),tint.a);
}'''

BOOTSTRAP = r'''
const sunProgram=gl.createProgram();
gl.attachShader(sunProgram,shader(gl.VERTEX_SHADER,`#version 300 es
layout(location=0) in vec3 p; layout(location=2) in mat4 model;
uniform mat4 sunVP; void main(){gl_Position=sunVP*model*vec4(p,1.);}`));
gl.attachShader(sunProgram,shader(gl.FRAGMENT_SHADER,`#version 300 es
precision highp float; void main(){}`));
gl.linkProgram(sunProgram);
if(!gl.getProgramParameter(sunProgram,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(sunProgram));
const sunDepth=gl.createTexture(), sunFramebuffer=gl.createFramebuffer();
gl.bindTexture(gl.TEXTURE_2D,sunDepth);
gl.texImage2D(gl.TEXTURE_2D,0,gl.DEPTH_COMPONENT24,2048,2048,0,gl.DEPTH_COMPONENT,gl.UNSIGNED_INT,null);
for(let parameter of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,parameter,gl.NEAREST);
for(let parameter of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,parameter,gl.CLAMP_TO_EDGE);
gl.bindFramebuffer(gl.FRAMEBUFFER,sunFramebuffer);
gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.TEXTURE_2D,sunDepth,0);
gl.drawBuffers([gl.NONE]);gl.readBuffer(gl.NONE);
if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)throw Error('Sun shadow framebuffer unavailable');
gl.bindFramebuffer(gl.FRAMEBUFFER,null);
let cachedSunKey='', sunVP;
function drawSunShadows(batches,width,height,eye){
    const enabled=document.getElementById('sunlight').checked;
    const wide=['exterior','floor'].includes(mode), key=[wide,mode==='floor'?floor:0,cutaway].join(':');
    if(key!==cachedSunKey){
        const center=[0,7,0], L=normalize([.4,.85,.5]), sunEye=center.map((v,i)=>v+L[i]*50), r=wide?48:25;
        const ortho=new Float32Array([1/r,0,0,0,0,1/r,0,0,0,0,-2/110,0,0,0,-1,1]);
        sunVP=multiply(ortho,viewMatrix(sunEye,center));
        gl.bindFramebuffer(gl.FRAMEBUFFER,sunFramebuffer);gl.viewport(0,0,2048,2048);
        gl.clear(gl.DEPTH_BUFFER_BIT);gl.disable(gl.BLEND);gl.depthMask(true);
        gl.enable(gl.POLYGON_OFFSET_FILL);gl.polygonOffset(1,1);
        gl.useProgram(sunProgram);gl.uniformMatrix4fv(gl.getUniformLocation(sunProgram,'sunVP'),false,sunVP);
        for(let[k,instances]of Object.entries(batches)){
            if(!instances.length)continue;
            const g=geometry[k];gl.bindVertexArray(g.vao);gl.bindBuffer(gl.ARRAY_BUFFER,g.instances);
            gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(instances),gl.DYNAMIC_DRAW);
            gl.drawArraysInstanced(gl.TRIANGLES,0,g.count,instances.length/21);
        }
        gl.disable(gl.POLYGON_OFFSET_FILL);gl.bindFramebuffer(gl.FRAMEBUFFER,null);
        gl.viewport(0,0,width,height);cachedSunKey=key;
    }
    gl.useProgram(program);
    gl.uniformMatrix4fv(gl.getUniformLocation(program,'sunVP'),false,sunVP);
    gl.uniform3fv(gl.getUniformLocation(program,'cameraEye'),eye);
    gl.uniform1i(gl.getUniformLocation(program,'sunShadows'),enabled?1:0);
    gl.activeTexture(gl.TEXTURE2);gl.bindTexture(gl.TEXTURE_2D,sunDepth);
    gl.uniform1i(gl.getUniformLocation(program,'sunDepth'),2);
    gl.uniform1fv(gl.getUniformLocation(program,'gloss'),new Float32Array(data.materials.map(m=>m.smoothness||0)));
    gl.uniform1fv(gl.getUniformLocation(program,'metal'),new Float32Array(data.materials.map(m=>m.metallic||0)));
}
'''


def upgrade_preview(template):
    pattern = r'gl.attachShader\(program,shader\(gl.FRAGMENT_SHADER,`#version 300 es.*?`\)\);'
    template, count = re.subn(pattern, lambda _: 'gl.attachShader(program,shader(gl.FRAGMENT_SHADER,`'+FRAGMENT+'`));', template, count=1, flags=re.S)
    assert count == 1, 'Missing preview surface shader'
    anchor = 'const textProgram=gl.createProgram();'
    assert anchor in template
    template = template.replace(anchor, BOOTSTRAP+'\n'+anchor, 1)
    template = template.replace('}addRouteBatches(batches);', '}drawSunShadows(batches,width,height,eye);addRouteBatches(batches);', 1)
    template = template.replace('<div class="line"></div>', '<label class="check"><input id="sunlight" type="checkbox" checked>太阳阴影</label><div class="line"></div>', 1)
    template = template.replace('window.preview={', "document.getElementById('sunlight').addEventListener('change',()=>dirty=true);window.preview={", 1)
    return template
