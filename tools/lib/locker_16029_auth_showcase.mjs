import { build16029ParametricContract } from './locker_16029_parametric_contract.mjs'

// This light display model follows the standard layout, not a CAD tessellation.
const standard = build16029ParametricContract({
  cabinet: { widthMm: 1000, heightMm: 1917, depthMm: 550 },
  columns: [
    { side: 'L', doors: [6, 4, 2].map(heightUnits => ({ heightUnits })) },
    { side: 'R', doors: [2, 4, 6].map(heightUnits => ({ heightUnits })) },
  ],
})

export function render16029AuthCabinet() {
  return `<div class="cabinet-showcase" id="authCabinetDemo" data-view="exterior">
    <div class="cabinet-viewport" id="authStudySurface" tabindex="0" role="group" aria-label="1000 × 1917 × 550 毫米标准柜三维展示。拖动旋转；滚轮缩放；方向键调整视角；加减键缩放；Home 复位。">
      <div class="cabinet-aura" aria-hidden="true"></div>
      <canvas id="authCabinetCanvas" role="img" aria-label="两列六门标准柜的三维展示模型，可开门查看错层层板、中间双隔板及门板折边。"></canvas>
      <svg class="cabinet-dimensions" id="authCabinetDimensions" aria-hidden="true"><g id="cabinetHeightDimension"></g><g id="cabinetWidthDimension"></g></svg>
      <span class="cabinet-door-tip" id="authDoorTip" hidden></span>
      <span class="cabinet-interaction-note"><svg viewBox="0 0 18 20" aria-hidden="true"><rect x="3.5" y="1.5" width="11" height="17" rx="5.5"/><path d="M9 2v5"/></svg>拖动旋转 · 滚轮缩放</span>
      <noscript><p class="cabinet-script-note">启用 JavaScript 可查看互动模型；也可以直接登录。</p></noscript>
    </div>
    <div class="cabinet-controls">
      <div class="cabinet-view-switch" role="group" aria-label="标准柜展示方式">
        <button type="button" data-product-view="exterior" aria-pressed="true">外观</button>
        <button type="button" data-product-view="interior" aria-pressed="false">开门看结构</button>
      </div>
      <button type="button" class="cabinet-reset" id="authCabinetReset" aria-label="复位视角和缩放" title="复位视角和缩放"><svg viewBox="0 0 20 20" aria-hidden="true"><path d="M4.2 7.5a6.4 6.4 0 1 1 .1 5.5M4.2 3v4.5H8.7"/></svg><span>复位</span></button>
    </div>
    <div class="cabinet-caption">
      <div><span class="cabinet-series">16029 / STANDARD CABINET</span><strong>1000 <i>×</i> 1917 <i>×</i> 550 <small>mm</small></strong></div>
      <p id="authStudyHint" aria-live="polite">两列六门 · 左右错层布局</p>
      <span class="cabinet-model-note">标准尺寸展示模型 · 非订单 CAD</span>
    </div>
  </div>`
}

export const authCabinetShowcaseCss = `
  .auth-study { background:linear-gradient(145deg,#eef0f3 0%,#e5e9ef 58%,#e7edf4 100%); }
  .auth-study-title h2 { font-size:clamp(38px,3.35vw,50px); }
  .auth-study-title p:last-child { max-width:240px; }
  .cabinet-showcase { min-width:0; display:flex; flex:1; flex-direction:column; position:relative; margin-top:-78px; }
  .cabinet-viewport { position:relative; flex:1; min-height:438px; isolation:isolate; border-radius:16px; outline-offset:-4px; cursor:grab; touch-action:pan-y; }
  .cabinet-viewport[data-dragging=true] { cursor:grabbing; }
  .cabinet-viewport:focus-visible { outline:2px solid #315bea80; }
  #authCabinetCanvas { display:block; position:absolute; inset:0; width:100%; height:100%; z-index:1; }
  .cabinet-aura { position:absolute; inset:8% -10% -4%; pointer-events:none; z-index:0; background:radial-gradient(ellipse at var(--light-x,65%) var(--light-y,48%),#ffffffa3 0%,#ffffff28 26%,transparent 58%),radial-gradient(ellipse at 58% 86%,#5889e826 0%,transparent 42%); transition:opacity .6s; opacity:.75; }
  .cabinet-showcase[data-active=true] .cabinet-aura { opacity:1; }
  .cabinet-dimensions { position:absolute; inset:0; width:100%; height:100%; z-index:2; pointer-events:none; overflow:visible; }
  .cabinet-dimensions line { stroke:#8a9aaf; stroke-width:.7; opacity:.65; }
  .cabinet-dimensions text { fill:#7b8aa0; font:10px var(--mono); letter-spacing:.025em; }
  .cabinet-door-tip { position:absolute; z-index:3; padding:8px 11px; background:#ffffffee; border:1px solid #d7dfea; border-radius:8px; color:#56677d; font:10px/1.65 var(--sans); pointer-events:none; box-shadow:0 4px 18px #2136550a; white-space:pre-line; backdrop-filter:blur(8px); }
  .cabinet-interaction-note { position:absolute; bottom:4px; left:0; z-index:2; display:flex; align-items:center; gap:6px; color:#8793a4; font-size:10px; pointer-events:none; }
  .cabinet-interaction-note svg { width:12px; height:15px; fill:none; stroke:currentColor; stroke-width:1.1; }
  .cabinet-controls { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-top:12px; }
  .cabinet-view-switch { display:flex; padding:3px; border-radius:10px; background:#d9e0e9; }
  .cabinet-view-switch button { min-height:44px; padding:8px 16px; color:#738096; background:transparent; border:0; border-radius:8px; font-size:11px; }
  .cabinet-view-switch button[aria-pressed=true] { color:#304563; background:#fff; box-shadow:0 2px 5px #233a5208; }
  .cabinet-view-switch button:hover { color:#315bea; }
  .cabinet-reset { min-height:44px; padding:8px 12px; gap:6px; border:1px solid #cbd4e0; border-radius:9px; color:#6d7e94; background:transparent; font-size:11px; }
  .cabinet-reset:hover { color:#315bea; border-color:#9cb3d7; background:#ffffff60; }
  .cabinet-reset svg { width:16px; height:16px; fill:none; stroke:currentColor; stroke-width:1.25; stroke-linecap:round; stroke-linejoin:round; }
  .cabinet-caption { margin-top:16px; padding-top:15px; border-top:1px solid #cfd7e1; display:grid; grid-template-columns:1fr auto; gap:7px 14px; align-items:end; }
  .cabinet-caption > div { display:grid; gap:5px; }
  .cabinet-series { color:#8a95a5; font:8px/1.5 var(--mono); letter-spacing:.05em; }
  .cabinet-caption strong { color:#52637b; font:400 17px/1.3 var(--mono); font-variant-numeric:tabular-nums; letter-spacing:-.035em; }
  .cabinet-caption strong i { padding:0 3px; font-style:normal; color:#9ba7b7; }
  .cabinet-caption strong small { font-size:10px; color:#8a95a5; }
  .cabinet-caption p { margin:0; text-align:right; color:#64758c; font-size:10px; }
  .cabinet-model-note { grid-column:1/-1; color:#8a95a5; font-size:9px; }
  .cabinet-script-note { position:relative; z-index:3; margin-top:120px; font-size:12px; }
  @media(min-width:1600px) { .cabinet-viewport { min-height:485px; } .cabinet-showcase { margin-top:-94px; } }
  @media(max-width:1200px) { .cabinet-showcase { margin-top:-42px; } .cabinet-viewport { min-height:395px; } .auth-study-title p:last-child { max-width:220px; } .cabinet-caption { grid-template-columns:1fr; } .cabinet-caption p { text-align:left; } }
  @media(max-width:1024px) { .cabinet-showcase { margin-top:-10px; } .cabinet-viewport { min-height:340px; } .auth-study-title h2 { font-size:40px; } }
  @media(max-width:820px) { .cabinet-showcase { margin-top:-112px; } .cabinet-viewport { min-height:455px; } .cabinet-caption { grid-template-columns:1fr auto; } .cabinet-caption p { text-align:right; } }
  @media(max-width:540px) { .cabinet-showcase { margin-top:0; } .cabinet-viewport { min-height:365px; } .auth-study-title h2 { font-size:38px; } .auth-study-title p:last-child { max-width:none; } .cabinet-caption { grid-template-columns:1fr; } .cabinet-caption p { text-align:left; } .cabinet-caption strong { font-size:16px; } .cabinet-view-switch button { padding:8px 12px; } .cabinet-reset { padding:8px 10px; } }
  @media(prefers-reduced-motion:reduce) { .cabinet-aura { transition:none; } }
`

export function render16029AuthCabinetScript() {
  return `<script>(${initializeCabinetShowcase.toString()})(${JSON.stringify({
    width: standard.geometry.cabinet.widthMm,
    height: standard.geometry.cabinet.heightMm,
    depth: standard.geometry.cabinet.depthMm,
    leafWidth: standard.geometry.doorLeafWidthMm,
    rows: ['L', 'R'].flatMap(side => standard.rowsByColumn[side].map(row => ({ ...row, centerXmm: standard.moduleDimensions.doors.centerXmm[side] }))),
    boundaries: standard.boundaries,
  })})</script>`
}

function initializeCabinetShowcase(spec) {
  const scene = document.getElementById('authCabinetDemo')
  const stage = document.getElementById('authStudySurface')
  let canvas = document.getElementById('authCabinetCanvas')
  const dimensions = document.getElementById('authCabinetDimensions')
  const hint = document.getElementById('authStudyHint')
  const tip = document.getElementById('authDoorTip')
  if (!scene || !stage || !canvas) return
  const reduced = matchMedia('(prefers-reduced-motion: reduce)')
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v))
  const norm = a => { const length = Math.hypot(...a) || 1; return a.map(v => v / length) }
  const cross = (a, b) => [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
  const dot = (a, b) => a.reduce((sum, v, i) => sum + v * b[i], 0)
  const identity = () => new Float32Array([1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1])
  function multiply(a, b) {
    const c = new Float32Array(16)
    for (let col=0; col<4; col++) for (let row=0; row<4; row++) for (let i=0; i<4; i++) c[col*4+row] += a[i*4+row]*b[col*4+i]
    return c
  }
  function lookAt(eye, target) {
    const z = norm(eye.map((v,i) => v-target[i])), x = norm(cross([0,1,0], z)), y = cross(z,x)
    return new Float32Array([x[0],y[0],z[0],0,x[1],y[1],z[1],0,x[2],y[2],z[2],0,-dot(x,eye),-dot(y,eye),-dot(z,eye),1])
  }
  function perspective(aspect) {
    const f = 1 / Math.tan(.53/2), near=.1, far=15
    return new Float32Array([f/aspect,0,0,0,0,f,0,0,0,0,(far+near)/(near-far),-1,0,0,2*far*near/(near-far),0])
  }
  function ortho(size, near, far) { return new Float32Array([1/size,0,0,0,0,1/size,0,0,0,0,-2/(far-near),0,0,0,-(far+near)/(far-near),1]) }
  function doorMatrix(door, angle) {
    const m = identity(), c = Math.cos(angle), s = Math.sin(angle)
    m[0]=c; m[2]=-s; m[8]=s; m[10]=c; m[12]=door.hinge; m[14]=.282
    return m
  }
  const vertex = (data, p, n, color, metal) => data.push(...p, ...n, ...color, metal)
  function box(data, center, size, color, metal=.1, bevel=0) {
    const half = size.map(v=>v/2), r = Math.min(bevel, ...half)
    for (let axis=0; axis<3; axis++) for (const sign of [-1,1]) {
      const u=(axis+1)%3, v=(axis+2)%3
      const us = r ? [-half[u], -half[u]+r, half[u]-r, half[u]] : [-half[u],half[u]]
      const vs = r ? [-half[v], -half[v]+r, half[v]-r, half[v]] : [-half[v],half[v]]
      const point = (a,b) => {
        const p=[0,0,0]; p[axis]=sign*half[axis]; p[u]=a; p[v]=b
        const core=p.map((t,i)=>clamp(t,-half[i]+r,half[i]-r))
        const n = r ? norm(p.map((t,i)=>t-core[i])) : [axis===0?sign:0,axis===1?sign:0,axis===2?sign:0]
        return { p:p.map((t,i)=>center[i]+(r ? core[i]+n[i]*r : t)), n }
      }
      for (let a=0;a<us.length-1;a++) for(let b=0;b<vs.length-1;b++) {
        const corners=[point(us[a],vs[b]),point(us[a+1],vs[b]),point(us[a+1],vs[b+1]),point(us[a],vs[b+1])]
        for (const i of sign===1 ? [0,1,2,0,2,3] : [0,2,1,0,3,2]) vertex(data,corners[i].p,corners[i].n,color,metal)
      }
    }
  }
  function cylinder(data, center, radius, depth, color, axis=2, metal=.7) {
    const u=(axis+1)%3, v=(axis+2)%3
    for (let i=0;i<24;i++) {
      const angles=[i/24*Math.PI*2,(i+1)/24*Math.PI*2]
      const rim=(a,sign)=>{const p=[...center],n=[0,0,0];p[axis]+=sign*depth/2;p[u]+=Math.cos(a)*radius;p[v]+=Math.sin(a)*radius;n[u]=Math.cos(a);n[v]=Math.sin(a);return {p,n}}
      const corners=[rim(angles[0],-1),rim(angles[1],-1),rim(angles[1],1),rim(angles[0],1)]
      for(const j of [0,1,2,0,2,3]) vertex(data,corners[j].p,corners[j].n,color,metal)
      for(const sign of [-1,1]) {
        const c=[...center],n=[0,0,0];c[axis]+=sign*depth/2;n[axis]=sign
        const a=rim(angles[0],sign),b=rim(angles[1],sign)
        for(const p of sign===1?[c,a.p,b.p]:[c,b.p,a.p]) vertex(data,p,n,color,metal)
      }
    }
  }
  const paint=[.80,.83,.88], inside=[.75,.79,.84], bright=[.88,.90,.94], steel=[.48,.55,.64], dark=[.18,.22,.28]
  const body=[], floor=[], halo=[]
  const W=spec.width/1000, H=spec.height/1000, D=spec.depth/1000
  // Folded sides, two center partitions, front returns, plinth and top cap.
  for(const side of [-1,1]) {
    box(body,[side*(W/2-.001), (H+.025)/2, 0],[.002,H-.025,D],paint,.22,.0005)
    box(body,[side*(W/2-.011), .947,.263],[.021,1.798,.023],bright,.25,.001)
    box(body,[side*.064,.946,-.002],[.0015,1.825,.53],inside,.18)
    box(body,[side*.0425,.945,.260],[.036,1.826,.025],paint,.23,.001)
    for(const z of [-.237,.207]) {
      box(body,[side*.074,.946,z],[.019,1.787,.0015],inside,.23)
      box(body,[side*.084,.946,z-.007],[.0015,1.787,.014],inside,.23)
    }
    for(const x of [side*.465]) for(const z of [-.226,.226]) {
      cylinder(body,[x,.0045,z],.021,.009,dark,1,.2)
      cylinder(body,[x,.016,z],.011,.019,steel,1,.7)
    }
    for(const y of [.064,.508,1.1,1.842]) cylinder(body,[side*.489,y,.278],.003,.0017,steel,2,.75)
  }
  box(body,[0,.972,-.274],[.996,1.886,.002],inside,.15)
  box(body,[0,.966,-.271],[.002,1.866,.002],steel,.12)
  box(body,[0,.030,0],[.998,.014,.544],inside,.2,.001)
  box(body,[0,.018,.252],[.961,.023,.023],paint,.25,.001)
  box(body,[0,H-.002,0],[1,.004,.55],bright,.22,.001)
  box(body,[0,1.885,.263],[.996,.060,.026],paint,.22,.0014)
  box(body,[0,1.912,-.258],[.996,.010,.019],paint,.25,.001)
  box(body,[0,1.888,0],[.92,.026,.032],inside,.15)
  box(body,[0,.027,-.246],[.957,.022,.029],inside,.15)
  // The center service gap is closed by its removable front panel.
  box(body,[0,.944,.278],[.074,1.817,.002],paint,.2,.0008)
  for(const y of [.083,1.8]) cylinder(body,[0,y,.280],.003,.0015,steel,2,.8)
  for(const side of ['L','R']) {
    const x=side==='L'?-.28:.28
    for(const boundary of spec.boundaries[side]) {
      const y=boundary.shelfCenterYmm/1000
      box(body,[x,y,-.008],[.429,.002,.508],bright,.18)
      box(body,[x,y-.010,.249],[.429,.022,.002],inside,.18)
      box(body,[x,y-.007,-.260],[.429,.016,.002],inside,.18)
      box(body,[x,y-.015,.202],[.391,.017,.016],inside,.18,.0008)
      box(body,[x,boundary.crossbarCenterYmm/1000,.266],[.445,.014,.019],paint,.2,.0008)
    }
    box(body,[x,.040,-.008],[.429,.002,.508],bright,.18)
  }
  const doors=spec.rows.map(row=>{
    const hinge=(row.side==='L'?-1:1)*.477
    const center=row.centerXmm/1000-hinge, width=spec.leafWidth/1000, height=row.doorHeightMm/1000, y=row.centerYmm/1000
    const data=[]
    box(data,[center,y,0],[width,height,.002],bright,.20,.0007)
    for(const x of [center-width/2+.002,center+width/2-.002]) {
      box(data,[x,y,-.009],[.002,height-.003,.017],paint,.22)
      box(data,[x+Math.sign(center)*.011,y,-.021],[.014,height-.012,.002],inside,.22)
    }
    for(const edgeY of [y-height/2+.002,y+height/2-.002]) box(data,[center,edgeY,-.009],[width-.003,.002,.017],paint,.2)
    for(const x of [center-width/2+.025,center+width/2-.025]) box(data,[x,y,-.020],[.018,height-.011,.010],inside,.23,.0007)
    const lockX=(row.side==='L'?-.055:.055)-hinge
    cylinder(data,[lockX,y,.0027],.0098,.0033,steel,2,.9)
    cylinder(data,[lockX,y,.0046],.0065,.001,bright,2,1)
    box(data,[lockX,y,.0055],[.0018,.007,.0006],dark,0)
    box(data,[lockX,y,-.027],[.023,.009,.003],steel,.6,.0004)
    const hingeX=(row.side==='L'?-.465:.465)-hinge
    for(const edgeY of [y-height/2+.03,y+height/2-.03]) {
      cylinder(data,[hingeX,edgeY,-.009],.005,.027,steel,1,.7)
      for(const delta of [-.009,.009]) cylinder(data,[hingeX,edgeY+delta,.002],.0025,.001,steel,2,.8)
    }
    return { ...row, hinge, width, height, center, y, data, open:0, target:0, matrix:identity() }
  })
  box(floor,[0,-.008,0],[6,.003,6],[0,0,0],0)
  for(const arc of [[.14,2.1],[2.48,4.32],[4.63,6.05]]) for(let i=0;i<76;i++) {
    const a=arc[0]+(arc[1]-arc[0])*i/76,b=arc[0]+(arc[1]-arc[0])*(i+1)/76
    const p=(angle,r)=>[Math.sin(angle)*r,.0025,Math.cos(angle)*r*.68]
    const points=[p(a,.79),p(b,.79),p(b,.798),p(a,.798)]
    for(const j of [0,1,2,0,2,3]) vertex(halo,points[j],[0,1,0],[.32,.52,.85],0)
  }
  const meshes=[{data:floor,mode:1,matrix:identity()},{data:halo,mode:2,matrix:identity()},{data:body,mode:0,matrix:identity()},...doors.map(door=>({data:door.data,mode:0,door}))]
  let renderer, disposed=false, visible=true, frameId=0, lastTime=0, dragging=null, hovered=-1, viewProjection=identity(), eye=[0,1,3]
  let yaw=.42, pitch=.28, zoom=1, targetYaw=yaw, targetPitch=pitch, targetZoom=zoom, lightX=-2.5, targetLightX=lightX
  const defaultYaw=.42, defaultPitch=.28
  function webglRenderer() {
    const gl=canvas.getContext('webgl',{alpha:true,antialias:true,premultipliedAlpha:true,preserveDrawingBuffer:true})
    if(!gl) throw new Error('3D context unavailable')
    const compile=(type,source)=>{const shader=gl.createShader(type);gl.shaderSource(shader,source);gl.compileShader(shader);if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(shader));return shader}
    const program=(vs,fs)=>{const p=gl.createProgram();gl.attachShader(p,compile(gl.VERTEX_SHADER,vs));gl.attachShader(p,compile(gl.FRAGMENT_SHADER,fs));gl.linkProgram(p);if(!gl.getProgramParameter(p,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(p));return p}
    const vertexSource=`attribute vec3 aPosition; attribute vec3 aNormal; attribute vec3 aColor; attribute float aMetal;
      uniform mat4 uModel; uniform mat4 uViewProjection; uniform mat4 uLightMatrix;
      varying vec3 vPosition; varying vec3 vNormal; varying vec3 vColor; varying float vMetal; varying vec4 vShadow;
      void main(){vec4 world=uModel*vec4(aPosition,1.0);vPosition=world.xyz;vNormal=mat3(uModel)*aNormal;vColor=aColor;vMetal=aMetal;vShadow=uLightMatrix*world;gl_Position=uViewProjection*world;}`
    const fragmentSource=`precision highp float; varying vec3 vPosition; varying vec3 vNormal; varying vec3 vColor; varying float vMetal; varying vec4 vShadow;
      uniform vec3 uEye; uniform vec3 uLight; uniform sampler2D uShadow; uniform float uShadowEnabled; uniform float uMode; uniform float uHighlight; uniform float uGlow;
      float shadow(vec3 n,vec3 l){if(uShadowEnabled<0.5)return 1.0;vec3 p=vShadow.xyz/vShadow.w*.5+.5;if(p.x<.001||p.x>.999||p.y<.001||p.y>.999||p.z>1.0)return 1.0;float amount=0.0;float bias=max(.0008,.002*(1.0-dot(n,l)));for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++){float depth=texture2D(uShadow,p.xy+vec2(float(x),float(y))/1024.0).r;amount+=step(p.z-bias,depth);}return amount/9.0;}
      void main(){vec3 n=normalize(vNormal),l=normalize(uLight-vPosition),view=normalize(uEye-vPosition);float shade=shadow(n,l);
        if(uMode>1.5){gl_FragColor=vec4(vColor,.18+.22*uGlow);return;}
        if(uMode>.5){float contact=exp(-dot(vPosition.xz*vec2(1.6,2.9),vPosition.xz*vec2(1.6,2.9))*2.0);float fade=1.0-smoothstep(.6,1.4,length(vPosition.xz));gl_FragColor=vec4(.22,.32,.47,((1.0-shade)*.17+contact*.12)*fade);return;}
        float key=max(dot(n,l),0.0);float fill=max(dot(n,normalize(vec3(2.8,1.6,2.0))),0.0);float rim=max(dot(n,normalize(vec3(1.2,2.5,-2.0))),0.0);
        float ambient=.59+.09*max(n.y,0.0);vec3 diffuse=vColor*(ambient+.43*key*(.55+.45*shade)+.13*fill+.10*rim);
        vec3 halfVector=normalize(l+view);float specular=pow(max(dot(n,halfVector),0.0),mix(52.0,105.0,vMetal));
        vec3 reflected=reflect(-view,n);float softbox=pow(max(0.0,1.0-abs(reflected.x+.38)*1.15),8.0)*pow(max(0.0,reflected.y*.4+.7),3.0);
        float fresnel=pow(1.0-max(dot(n,view),0.0),3.0);float grain=fract(sin(dot(vPosition.xy,vec2(9128.7,57223.3)))*43758.5453)*.0015;
        vec3 color=diffuse+vec3(.21,.23,.26)*specular*shade+vec3(.10,.13,.17)*softbox*vMetal+vec3(.075,.10,.15)*fresnel+grain;
        color=mix(color,vec3(.58,.72,.93),uHighlight*.17);gl_FragColor=vec4(color,1.0);}`
    const main=program(vertexSource,fragmentSource)
    const depth=program(`attribute vec3 aPosition;uniform mat4 uModel;uniform mat4 uViewProjection;void main(){gl_Position=uViewProjection*uModel*vec4(aPosition,1.0);}`,`precision mediump float;void main(){gl_FragColor=vec4(1.0);}`)
    const programs=[main,depth].map(p=>({p,attributes:Object.fromEntries(['aPosition','aNormal','aColor','aMetal'].map(k=>[k,gl.getAttribLocation(p,k)])),uniforms:Object.fromEntries(['uModel','uViewProjection','uLightMatrix','uEye','uLight','uShadow','uShadowEnabled','uMode','uHighlight','uGlow'].map(k=>[k,gl.getUniformLocation(p,k)]))}))
    const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1024,1024,0,gl.RGBA,gl.UNSIGNED_BYTE,null);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE)
    const fb=gl.createFramebuffer(),depthBuffer=gl.createRenderbuffer(),depthTexture=gl.getExtension('WEBGL_depth_texture')?gl.createTexture():null
    gl.bindFramebuffer(gl.FRAMEBUFFER,fb);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,texture,0)
    if(depthTexture){gl.bindTexture(gl.TEXTURE_2D,depthTexture);gl.texImage2D(gl.TEXTURE_2D,0,gl.DEPTH_COMPONENT,1024,1024,0,gl.DEPTH_COMPONENT,gl.UNSIGNED_SHORT,null);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.TEXTURE_2D,depthTexture,0)}else{gl.bindRenderbuffer(gl.RENDERBUFFER,depthBuffer);gl.renderbufferStorage(gl.RENDERBUFFER,gl.DEPTH_COMPONENT16,1024,1024);gl.framebufferRenderbuffer(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.RENDERBUFFER,depthBuffer)}
    const shadows=Boolean(depthTexture)&&gl.checkFramebufferStatus(gl.FRAMEBUFFER)===gl.FRAMEBUFFER_COMPLETE
    gl.bindFramebuffer(gl.FRAMEBUFFER,null)
    for(const mesh of meshes){mesh.buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,mesh.buffer);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(mesh.data),gl.STATIC_DRAW)}
    const bind=(mesh,info)=>{
      gl.bindBuffer(gl.ARRAY_BUFFER,mesh.buffer)
      for(const [name,length,offset] of [['aPosition',3,0],['aNormal',3,12],['aColor',3,24],['aMetal',1,36]]) if(info.attributes[name]>=0){gl.enableVertexAttribArray(info.attributes[name]);gl.vertexAttribPointer(info.attributes[name],length,gl.FLOAT,false,40,offset)}
      gl.uniformMatrix4fv(info.uniforms.uModel,false,mesh.door?mesh.door.matrix:mesh.matrix)
      gl.drawArrays(gl.TRIANGLES,0,mesh.data.length/10)
    }
    return {
      kind:'webgl',
      draw(){
        const light=[lightX,4.1,3.6],lightMatrix=multiply(ortho(1.8,.1,10),lookAt(light,[0,.86,0]))
        gl.enable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE)
        gl.disable(gl.DITHER)
        if(shadows){gl.bindFramebuffer(gl.FRAMEBUFFER,fb);gl.viewport(0,0,1024,1024);gl.disable(gl.BLEND);gl.clearColor(1,1,1,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);gl.useProgram(depth);gl.uniformMatrix4fv(programs[1].uniforms.uViewProjection,false,lightMatrix);for(const mesh of meshes)if(mesh.mode===0)bind(mesh,programs[1])}
        gl.bindFramebuffer(gl.FRAMEBUFFER,null);gl.viewport(0,0,canvas.width,canvas.height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);gl.useProgram(main)
        const u=programs[0].uniforms
        gl.uniformMatrix4fv(u.uViewProjection,false,viewProjection);gl.uniformMatrix4fv(u.uLightMatrix,false,lightMatrix);gl.uniform3fv(u.uEye,eye);gl.uniform3fv(u.uLight,light);gl.uniform1f(u.uShadowEnabled,shadows?1:0);gl.uniform1f(u.uGlow,scene.dataset.active==='true'&&!reduced.matches?1:0);gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,depthTexture||texture);gl.uniform1i(u.uShadow,0)
        for(const mesh of meshes){gl.uniform1f(u.uMode,mesh.mode);gl.uniform1f(u.uHighlight,mesh.door&&doors.indexOf(mesh.door)===hovered?1:0);bind(mesh,programs[0])}
      },
      destroy(){for(const mesh of meshes)gl.deleteBuffer(mesh.buffer);gl.deleteFramebuffer(fb);gl.deleteRenderbuffer(depthBuffer);gl.deleteTexture(texture);if(depthTexture)gl.deleteTexture(depthTexture);for(const p of programs)gl.deleteProgram(p.p)}
    }
  }
  function projected(p, matrix=identity()) {
    const m=multiply(viewProjection,matrix),v=[...p,1],q=[0,0,0,0]
    for(let r=0;r<4;r++)for(let i=0;i<4;i++)q[r]+=m[i*4+r]*v[i]
    return { x:(q[0]/q[3]+1)*stage.clientWidth/2,y:(1-q[1]/q[3])*stage.clientHeight/2,z:q[2]/q[3] }
  }
  function canvasRenderer() {
    const replacement=canvas.cloneNode(false);canvas.replaceWith(replacement);canvas=replacement
    const ctx=canvas.getContext('2d')
    return {kind:'canvas',draw(){
      if(!ctx)return
      const ratio=canvas.width/stage.clientWidth
      ctx.setTransform(ratio,0,0,ratio,0,0);ctx.clearRect(0,0,stage.clientWidth,stage.clientHeight)
      const ground=projected([0,0,0]),gradient=ctx.createRadialGradient(ground.x,ground.y,0,ground.x,ground.y,150)
      gradient.addColorStop(0,'#39547225');gradient.addColorStop(1,'#39547200');ctx.fillStyle=gradient;ctx.save();ctx.translate(0,ground.y*.56);ctx.scale(1,.44);ctx.beginPath();ctx.arc(ground.x,ground.y,150,0,Math.PI*2);ctx.fill();ctx.restore()
      const triangles=[]
      for(const mesh of meshes)if(mesh.mode===0){const m=mesh.door?mesh.door.matrix:mesh.matrix,d=mesh.data;for(let i=0;i<d.length;i+=30){const points=[0,10,20].map(k=>projected(d.slice(i+k,i+k+3),m));const n=[m[0]*d[i+3]+m[8]*d[i+5],d[i+4],m[2]*d[i+3]+m[10]*d[i+5]],shade=.67+.25*Math.max(0,dot(n,norm([-2,4,3])));triangles.push({points,z:points.reduce((s,p)=>s+p.z,0)/3,color:'rgb('+d.slice(i+6,i+9).map(c=>Math.round(clamp(c*shade,0,1)*255)).join(',')+')'})}}
      triangles.sort((a,b)=>b.z-a.z)
      for(const triangle of triangles){ctx.beginPath();triangle.points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();ctx.fillStyle=triangle.color;ctx.fill()}
    },destroy(){}}
  }
  function initializeRenderer(){try{renderer=webglRenderer()}catch{renderer=canvasRenderer()}scene.dataset.renderer=renderer.kind;canvas.addEventListener('webglcontextlost',contextLost);canvas.addEventListener('webglcontextrestored',contextRestored)}
  function contextLost(event){event.preventDefault();cancelAnimationFrame(frameId);frameId=0;scene.dataset.renderer='recovering'}
  function contextRestored(){initializeRenderer();wake()}
  function drawDimensions(){
    dimensions.setAttribute('viewBox','0 0 '+stage.clientWidth+' '+stage.clientHeight)
    const a=projected([-.60,0,.28]),b=projected([-.60,H,.28]),c=projected([-.5,-.06,.35]),d=projected([.5,-.06,.35])
    const line=(p,q)=>'<line x1="'+p.x.toFixed(1)+'" y1="'+p.y.toFixed(1)+'" x2="'+q.x.toFixed(1)+'" y2="'+q.y.toFixed(1)+'" />'
    document.getElementById('cabinetHeightDimension').innerHTML=line(a,b)+line({x:a.x-4,y:a.y},{x:a.x+4,y:a.y})+line({x:b.x-4,y:b.y},{x:b.x+4,y:b.y})+'<text text-anchor="end" x="'+(Math.min(a.x,b.x)-9).toFixed(1)+'" y="'+((a.y+b.y)/2).toFixed(1)+'">1917</text>'
    document.getElementById('cabinetWidthDimension').innerHTML=line(c,d)+line({x:c.x,y:c.y-4},{x:c.x,y:c.y+4})+line({x:d.x,y:d.y-4},{x:d.x,y:d.y+4})+'<text text-anchor="middle" x="'+((c.x+d.x)/2).toFixed(1)+'" y="'+((c.y+d.y)/2+16).toFixed(1)+'">1000 mm</text>'
    dimensions.style.opacity=Math.abs(Math.sin(yaw))>.92||scene.dataset.view==='interior'?'0':'.8'
  }
  function updateCamera(){
    const aspect=stage.clientWidth/stage.clientHeight
    const distance=Math.max(4.75,3.55/Math.max(aspect,.45))/zoom
    eye=[Math.sin(yaw)*Math.cos(pitch)*distance,.94+Math.sin(pitch)*distance,Math.cos(yaw)*Math.cos(pitch)*distance]
    const p=perspective(aspect);p[8]=stage.clientWidth>510?-.16:0
    viewProjection=multiply(p,lookAt(eye,[0,.94,0]))
  }
  function tick(now){
    frameId=0;if(disposed||!visible||document.hidden)return
    const dt=lastTime?Math.min((now-lastTime)/1000,.05):.016;lastTime=now
    const ease=reduced.matches?1:1-Math.exp(-dt*12)
    yaw+=(targetYaw-yaw)*ease;pitch+=(targetPitch-pitch)*ease;zoom+=(targetZoom-zoom)*ease;lightX+=(targetLightX-lightX)*ease
    let moving=Math.abs(targetYaw-yaw)+Math.abs(targetPitch-pitch)+Math.abs(targetZoom-zoom)+Math.abs(targetLightX-lightX)>.001
    for(const door of doors){door.open+=(door.target-door.open)*ease;if(Math.abs(door.target-door.open)>.001)moving=true;door.matrix=doorMatrix(door,(door.side==='L'?-1:1)*door.open*1.91)}
    updateCamera();renderer.draw();drawDimensions()
    scene.dataset.yaw=yaw.toFixed(3);scene.dataset.zoom=zoom.toFixed(3);scene.dataset.open=doors.reduce((s,d)=>s+d.open,0).toFixed(3);scene.dataset.ready='true'
    if(moving)frameId=requestAnimationFrame(tick)
  }
  function wake(){if(!frameId&&!disposed&&visible&&!document.hidden&&scene.dataset.renderer!=='recovering')frameId=requestAnimationFrame(tick)}
  function resize(){const ratio=renderer.kind==='webgl'?Math.min(Math.max(devicePixelRatio||1,1.5),2):Math.min(devicePixelRatio||1,1.5),w=Math.round(stage.clientWidth*ratio),h=Math.round(stage.clientHeight*ratio);if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;if(!visible&&!disposed&&!document.hidden){for(const door of doors)door.matrix=doorMatrix(door,(door.side==='L'?-1:1)*door.open*1.91);updateCamera();renderer.draw();drawDimensions()}}wake()}
  function clearHover(){hovered=-1;tip.hidden=true}
  function reset(){targetYaw=defaultYaw;targetPitch=defaultPitch;targetZoom=1;targetLightX=-2.5;clearHover();wake()}
  function show(view){scene.dataset.view=view;for(const door of doors)door.target=view==='interior'?1:0;scene.querySelectorAll('[data-product-view]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.productView===view)));hint.textContent=view==='interior'?'开门查看 · 错层层板 / 双隔板 / 门板折边':'两列六门 · 左右错层布局';clearHover();if(view==='interior'){targetYaw=.18;targetZoom=.92}else{targetYaw=defaultYaw;targetZoom=1}wake()}
  function inPolygon(x,y,points){let inside=false;for(let i=0,j=points.length-1;i<points.length;j=i++){const a=points[i],b=points[j];if((a.y>y)!==(b.y>y)&&x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x)inside=!inside}return inside}
  function hover(event){
    if(event.pointerType==='touch'||dragging){clearHover();return}
    const bounds=stage.getBoundingClientRect(),x=event.clientX-bounds.left,y=event.clientY-bounds.top
    const found=[]
    for(const [index,door]of doors.entries()){
      const normal=[door.matrix[8],0,door.matrix[10]],worldCenter=[door.hinge+Math.cos((door.side==='L'?-1:1)*door.open*1.91)*door.center,door.y,.282+door.matrix[2]*door.center]
      if(dot(normal,eye.map((v,i)=>v-worldCenter[i]))<=0)continue
      const corners=[[-1,-1],[1,-1],[1,1],[-1,1]].map(([a,b])=>projected([door.center+a*door.width/2,door.y+b*door.height/2,.003],door.matrix))
      if(inPolygon(x,y,corners))found.push({index,z:corners.reduce((s,p)=>s+p.z,0)/4})
    }
    found.sort((a,b)=>a.z-b.z);hovered=found.length?found[0].index:-1
    if(hovered>=0){const door=doors[hovered];tip.textContent=(door.side==='L'?'左列':'右列')+' · '+String(door.index).padStart(2,'0')+'（从下往上）\n'+spec.leafWidth+' × '+door.doorHeightMm+' mm';tip.hidden=false;tip.style.left=clamp(x+15,8,stage.clientWidth-tip.offsetWidth-8)+'px';tip.style.top=clamp(y+15,8,stage.clientHeight-tip.offsetHeight-8)+'px'}else tip.hidden=true
  }
  scene.querySelectorAll('[data-product-view]').forEach(button=>button.addEventListener('click',()=>show(button.dataset.productView)))
  document.getElementById('authCabinetReset').addEventListener('click',reset)
  stage.addEventListener('pointerdown',event=>{if(event.button!==0||dragging)return;dragging={id:event.pointerId,x:event.clientX,y:event.clientY,yaw:targetYaw,pitch:targetPitch,touch:event.pointerType==='touch',active:event.pointerType!=='touch'};if(dragging.active){stage.setPointerCapture(event.pointerId);stage.dataset.dragging='true'}clearHover();wake()})
  stage.addEventListener('pointermove',event=>{
    scene.dataset.active='true'
    const bounds=stage.getBoundingClientRect(),dx=(event.clientX-bounds.left)/bounds.width,dy=(event.clientY-bounds.top)/bounds.height
    if(!reduced.matches){stage.style.setProperty('--light-x',(35+dx*45)+'%');stage.style.setProperty('--light-y',(25+dy*35)+'%');targetLightX=-3.2+dx*1.6}
    if(dragging&&dragging.id===event.pointerId){const moveX=event.clientX-dragging.x,moveY=event.clientY-dragging.y;if(!dragging.active&&Math.abs(moveX)>8&&Math.abs(moveX)>Math.abs(moveY)){dragging.active=true;stage.setPointerCapture(event.pointerId);stage.dataset.dragging='true'}if(dragging.active){targetYaw=dragging.yaw+moveX*.006;targetPitch=clamp(dragging.pitch+moveY*.004,-.22,.58);clearHover()}}else hover(event)
    wake()
  })
  const endDrag=event=>{if(dragging&&dragging.id===event.pointerId){if(stage.hasPointerCapture(event.pointerId))stage.releasePointerCapture(event.pointerId);dragging=null;stage.dataset.dragging='false';wake()}}
  stage.addEventListener('pointerup',endDrag);stage.addEventListener('pointercancel',endDrag);stage.addEventListener('lostpointercapture',event=>{if(event.target===stage)endDrag(event)})
  stage.addEventListener('pointerleave',()=>{scene.dataset.active='false';if(!dragging){targetLightX=-2.5;clearHover();wake()}})
  stage.addEventListener('wheel',event=>{event.preventDefault();targetZoom=clamp(targetZoom*Math.exp(-clamp(event.deltaY,-120,120)*.0015),.78,1.25);clearHover();wake()},{passive:false})
  stage.addEventListener('dblclick',reset)
  stage.addEventListener('keydown',event=>{
    if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home','0','+','=','-','_'].includes(event.key)){event.preventDefault();if(['Home','0'].includes(event.key))reset();else if(event.key==='ArrowLeft')targetYaw-=.16;else if(event.key==='ArrowRight')targetYaw+=.16;else if(event.key==='ArrowUp')targetPitch=clamp(targetPitch+.08,-.22,.58);else if(event.key==='ArrowDown')targetPitch=clamp(targetPitch-.08,-.22,.58);else targetZoom=clamp(targetZoom+(['+','='].includes(event.key)?.06:-.06),.78,1.25);clearHover();wake()}
  })
  reduced.addEventListener('change',()=>{targetLightX=-2.5;stage.style.removeProperty('--light-x');stage.style.removeProperty('--light-y');wake()})
  initializeRenderer()
  const resizeObserver=new ResizeObserver(resize);resizeObserver.observe(stage)
  const intersectionObserver=new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;if(visible){lastTime=0;wake()}else{cancelAnimationFrame(frameId);frameId=0}});intersectionObserver.observe(stage)
  document.addEventListener('visibilitychange',()=>{if(document.hidden){cancelAnimationFrame(frameId);frameId=0}else{lastTime=0;wake()}})
  window.addEventListener('pagehide',event=>{cancelAnimationFrame(frameId);frameId=0;if(!event.persisted){disposed=true;resizeObserver.disconnect();intersectionObserver.disconnect();renderer.destroy()}})
  window.addEventListener('pageshow',event=>{if(event.persisted){lastTime=0;resize();wake()}})
  resize()
}
