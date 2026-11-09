// Display geometry only. Task specification and native generation remain in the qualified v16 workflow.
export function outdoorStudioPreview(reference) {
  let canvas = document.getElementById('cabinetPreview')
  const stage = canvas.parentElement, overlay = document.getElementById('previewDimensions')
  const rotation = document.getElementById('previewRotation'), zoomControl = document.getElementById('previewZoom')
  const scene = stage, reduced = matchMedia('(prefers-reduced-motion: reduce)')
  let selectedView = 'iso', pitch = .23, dragging = null, frame = 0, disposed = false, signature = ''
  let eye = [0, 1, 4], viewProjection, renderer, width = 1, height = 1, current, doorLabels = []
  const lightX = -3.2
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

  const paint = [.76, .79, .83], bright = [.84, .86, .89], steel = [.45, .52, .61], dark = [.21, .26, .32]
  const meshes = [{ data: [], mode: 1, matrix: identity() }, { data: [], mode: 0, matrix: identity() }]
  box(meshes[0].data, [0, -.006, 0], [8, .004, 8], [0, 0, 0], 0)
  function readSpec() {
    const mm = (id, fallback) => { const n = Number(document.getElementById(id).value); return Number.isFinite(n) && n > 0 ? n : fallback }
    let columns = []
    try { columns = JSON.parse(document.getElementById('doorColumnsPayload').value) } catch {}
    return { W: mm('width', 950) / 1000, H: mm('height', 1939) / 1000, D: mm('depth', 550) / 1000, columns }
  }
  function build(spec) {
    const { W, H, D, columns } = spec, data = meshes[1].data = []
    const bodyH = H - .04125, doorW = W / 2 - .026
    box(data, [0, bodyH / 2, 0], [W, bodyH, D], paint, .20, .0012)
    // A recessed dark front supplies physical 6 mm reveals between separate door leaves.
    box(data, [0, (H - .075) / 2, D / 2 + .001], [W - .016, H - .105, .003], dark, .1)
    for (const side of [-1, 1]) box(data, [side * (W / 2 - .005), bodyH / 2, D / 2 + .004], [.010, bodyH, .012], paint, .20, .0006)
    box(data, [0, .011, D / 2 + .004], [W - .012, .022, .014], steel, .24, .0008)
    box(data, [0, H - .066, D / 2 + .004], [W - .012, .050, .012], paint, .20, .0008)
    box(data, [0, (H - .075) / 2, D / 2 + .004], [.020, H - .105, .012], paint, .2, .0007)
    box(data, [0, bodyH - .001, 0], [W + .001, .003, D + .002], bright, .25, .0006)
    doorLabels = []
    columns.forEach((column, col) => {
      let y = .023
      column.doors.forEach((door, index) => {
        const h = door.heightMm / 1000
        if (!Number.isFinite(h) || h <= 0) return
        const x = (col ? 1 : -1) * (W / 4 - .0015), cy = y + h / 2
        box(data, [x, cy, D / 2 + .009], [doorW, h, .014], bright, .18, .0011)
        const lockX = x + (col ? -1 : 1) * (doorW / 2 - .027)
        cylinder(data, [lockX, cy, D / 2 + .018], .0085, .004, steel, 2, .8)
        cylinder(data, [lockX, cy, D / 2 + .0202], .0055, .001, [.68, .72, .77], 2, .85)
        box(data, [lockX, cy, D / 2 + .021], [.0015, .007, .0008], dark, .1)
        doorLabels.push({ point: [x, cy, D / 2 + .024], text: column.side + String(index + 1).padStart(2, '0') + ' · ' + door.heightMm })
        y += h + .006
      })
    })
    // Accepted reference canopy surfaces retain the standard pillars; only their display position moves.
    for (const mesh of reference.meshes) {
      const span = mesh.span, dw = W - .950
      const color = /塑料/.test(mesh.document) ? [.88,.90,.92] : /遮阳板/.test(mesh.document) ? [.70,.75,.81] : [.57,.64,.72]
      const points = mesh.points.map(p => [mesh.rule === 'side' ? p[0] + mesh.side * dw / 2 : p[0] * Math.max(.02, span + dw) / span, p[1] + H - 1.939, p[2] + D / 2])
      for (const face of mesh.triangles) {
        const a = points[face[0]], b = points[face[1]], c = points[face[2]]
        const normal = norm(cross(b.map((v,i) => v-a[i]), c.map((v,i) => v-a[i])))
        for (const p of [a,b,c]) vertex(data, p, normal, color, .32)
      }
    }
    canvas.dataset.doorCount = String(doorLabels.length)
    canvas.dataset.canopySource = 'accepted-native-reference'
  }
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
      update(){for(const mesh of meshes){gl.bindBuffer(gl.ARRAY_BUFFER,mesh.buffer);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(mesh.data),gl.STATIC_DRAW)}},
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

  function projected(p) {
    const m = viewProjection, v = [...p, 1], q = [0,0,0,0]
    for (let r=0; r<4; r++) for (let i=0; i<4; i++) q[r] += m[i*4+r] * v[i]
    return { x: (q[0]/q[3]+1)*width/2, y: (1-q[1]/q[3])*height/2, z: q[2]/q[3] }
  }
  function camera() {
    const { W, H, D } = current, yaw = -Number(rotation.value)*Math.PI/180
    const center = [0, (H+.27)/2, .08], cp = Math.cos(pitch), sp = Math.sin(pitch)
    eye = [center[0]+4*Math.sin(yaw)*cp, center[1]+4*sp, center[2]+4*Math.cos(yaw)*cp]
    const view = lookAt(eye, center)
    const points = []
    for (const x of [-W/2-.08, W/2+.08]) for (const y of [0,H+.27]) for (const z of [-D/2-.18,D/2+.46]) {
      points.push([view[0]*x+view[4]*y+view[8]*z+view[12], view[1]*x+view[5]*y+view[9]*z+view[13]])
    }
    const minX=Math.min(...points.map(p=>p[0])),maxX=Math.max(...points.map(p=>p[0])),minY=Math.min(...points.map(p=>p[1])),maxY=Math.max(...points.map(p=>p[1]))
    const scale = Math.min(width*.75/(maxX-minX), (height-120)/(maxY-minY)) * Number(zoomControl.value)/100
    const sx = 2*scale/width, sy = 2*scale/height
    const projection = new Float32Array([sx,0,0,0,0,sy,0,0,0,0,-2/12,0,-(minX+maxX)*scale/width,-(minY+maxY)*scale/height-.015,-1,1])
    viewProjection = multiply(projection, view)
  }
  function dimensions() {
    const { W,H,D,columns } = current, elements = []
    const line = (x1,y1,x2,y2) => `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"/>`
    const text = (x,y,value,cls='') => `<text class="${cls}" x="${x}" y="${y}" text-anchor="middle">${value}</text>`
    function dimension(a,b,label,dx,dy) {
      const p=projected(a), q=projected(b), x1=p.x+dx,y1=p.y+dy,x2=q.x+dx,y2=q.y+dy
      elements.push(line(p.x,p.y,x1,y1),line(q.x,q.y,x2,y2),line(x1,y1,x2,y2))
      for (const [x,y] of [[x1,y1],[x2,y2]]) elements.push(line(x-3,y-3,x+3,y+3))
      elements.push(text((x1+x2)/2,(y1+y2)/2-5,label))
    }
    if (Number(zoomControl.value)<=110 && ['iso','front'].includes(selectedView)) dimension([-W/2,0,D/2],[W/2,0,D/2],`W ${+(W*1000).toFixed(3)}`,0,22)
    if (selectedView==='front' && Number(zoomControl.value)<=110) {
      for (const label of doorLabels) { const p=projected(label.point);elements.push(text(p.x,p.y+3,label.text,'door-number')) }
    }
    if (selectedView==='top' && Number(zoomControl.value)<=110) dimension([-W/2,0,-D/2],[W/2,0,-D/2],`W ${+(W*1000).toFixed(3)}`,0,22)
    elements.push(text(width/2,height-17,`${+(W*1000).toFixed(3)} W × ${+(H*1000).toFixed(3)} H × ${+(D*1000).toFixed(3)} D / mm`))
    overlay.setAttribute('viewBox',`0 0 ${width} ${height}`)
    overlay.innerHTML=elements.join('')
    document.getElementById('rotationLabel').textContent=rotation.value+'°'
    document.getElementById('zoomLabel').textContent=zoomControl.value+'%'
    document.getElementById('hudCabinetSize').textContent=[W,H,D].map(v=>+(v*1000).toFixed(3)).join(' × ')+' mm'
    document.getElementById('hudDoorLayout').textContent=`左 ${columns[0]?.doors.length||0} / 右 ${columns[1]?.doors.length||0} · 共 ${doorLabels.length} 门`
    document.getElementById('hudDoorSize').textContent=+(W*500-26).toFixed(3)+' mm'
    document.getElementById('hudLayoutState').textContent=document.getElementById('request-form').dataset.layoutValid==='true'?'两列均已闭合 · 门高 ≥170 mm':'布局待调整'
  }
  // Depth-buffered software fallback preserves door occlusion on machines without WebGL.
  function softwareRenderer() {
    const replacement = canvas.cloneNode(false); canvas.replaceWith(replacement); canvas = replacement
    const ctx = canvas.getContext('2d')
    document.getElementById('previewFallback').hidden=false
    return { kind:'software', update(){}, destroy(){}, draw(){
      const w=canvas.width,h=canvas.height,ratio=w/width,pixels=ctx.createImageData(w,h),depth=new Float32Array(w*h).fill(Infinity)
      const data=meshes[1].data, light=norm([-3,4,4])
      for(let i=0;i<data.length;i+=30){
        const points=[0,10,20].map(k=>projected(data.slice(i+k,i+k+3))).map(p=>({x:p.x*ratio,y:p.y*ratio,z:p.z}))
        const [a,b,c]=points,den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y)
        if(Math.abs(den)<.001)continue
        const n=data.slice(i+3,i+6),shade=.67+.34*Math.max(0,dot(n,light)),color=data.slice(i+6,i+9).map(v=>Math.round(clamp(v*shade,0,1)*255))
        const minX=Math.max(0,Math.floor(Math.min(a.x,b.x,c.x))),maxX=Math.min(w-1,Math.ceil(Math.max(a.x,b.x,c.x))),minY=Math.max(0,Math.floor(Math.min(a.y,b.y,c.y))),maxY=Math.min(h-1,Math.ceil(Math.max(a.y,b.y,c.y)))
        for(let y=minY;y<=maxY;y++)for(let x=minX;x<=maxX;x++){
          const u=((b.y-c.y)*(x+.5-c.x)+(c.x-b.x)*(y+.5-c.y))/den,v=((c.y-a.y)*(x+.5-c.x)+(a.x-c.x)*(y+.5-c.y))/den,t=1-u-v
          if(u<-.00001||v<-.00001||t<-.00001)continue
          const z=u*a.z+v*b.z+t*c.z,offset=y*w+x
          if(z<depth[offset]){depth[offset]=z;pixels.data.set([...color,255],offset*4)}
        }
      }
      ctx.putImageData(pixels,0,0)
    }}
  }
  function initialize() {
    try { renderer=webglRenderer() } catch { renderer=softwareRenderer() }
    canvas.dataset.renderer=renderer.kind
    canvas.addEventListener('webglcontextlost',event=>{event.preventDefault();canvas.dataset.renderer='recovering'})
    canvas.addEventListener('webglcontextrestored',()=>{renderer=webglRenderer();canvas.dataset.renderer=renderer.kind;schedule()})
    attachCanvas()
  }
  function draw() {
    frame=0
    if(disposed||!stage.clientWidth||!stage.clientHeight||canvas.dataset.renderer==='recovering')return
    current=readSpec();const next=JSON.stringify(current)
    if(next!==signature){build(current);signature=next;if(renderer)renderer.update()}
    if(!renderer)initialize()
    width=stage.clientWidth;height=stage.clientHeight
    const ratio=renderer.kind==='webgl'?Math.min(2,Math.max(1.5,devicePixelRatio||1)):1
    const w=Math.round(width*ratio),h=Math.round(height*ratio)
    if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h}
    camera();renderer.draw();dimensions()
    canvas.dataset.renderVersion=String(Number(canvas.dataset.renderVersion||0)+1)
    canvas.dataset.previewRevision='v17-native-canopy'
  }
  function schedule(){if(!frame&&!disposed)frame=requestAnimationFrame(draw)}
  function updateButtons() {
    document.querySelectorAll('[data-view]').forEach(button=>{button.classList.toggle('view-active',button.dataset.view===selectedView);button.setAttribute('aria-pressed',String(button.dataset.view===selectedView))})
  }
  function view(name) {
    selectedView=name;const values={iso:[-28,.23],front:[0,0],right:[-90,0],top:[0,Math.PI/2-.0001]}[name]
    rotation.value=values[0];pitch=values[1];updateButtons();schedule()
  }
  function custom(){selectedView='custom';if(pitch>1.55)pitch=.23;updateButtons();schedule()}
  function reset(){zoomControl.value=100;view('iso')}
  function attachCanvas() {
    canvas.addEventListener('pointerdown',event=>{if(event.button!==0)return;dragging={x:event.clientX,y:event.clientY,yaw:Number(rotation.value),pitch};canvas.setPointerCapture(event.pointerId)})
    canvas.addEventListener('pointermove',event=>{if(!dragging)return;rotation.value=clamp(dragging.yaw+(event.clientX-dragging.x)/3,-180,180);pitch=clamp(dragging.pitch+(event.clientY-dragging.y)/300,-.15,1.1);selectedView='custom';updateButtons();schedule()})
    for(const event of ['pointerup','pointercancel','lostpointercapture'])canvas.addEventListener(event,()=>{dragging=null})
    canvas.addEventListener('wheel',event=>{event.preventDefault();zoomControl.value=clamp(Number(zoomControl.value)+(event.deltaY>0?-5:5),70,150);schedule()},{passive:false})
    canvas.addEventListener('dblclick',reset)
    canvas.addEventListener('keydown',event=>{
      if(!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','+','-','=','Home'].includes(event.key))return
      event.preventDefault()
      if(event.key==='Home'){reset();return}
      if(event.key==='ArrowLeft'||event.key==='ArrowRight')rotation.value=clamp(Number(rotation.value)+(event.key==='ArrowLeft'?-5:5),-180,180)
      if(event.key==='ArrowUp'||event.key==='ArrowDown')pitch=clamp(pitch+(event.key==='ArrowUp'?.06:-.06),-.15,1.1)
      if(['+','=','-'].includes(event.key))zoomControl.value=clamp(Number(zoomControl.value)+(event.key==='-'?-5:5),70,150)
      custom()
    })
  }
  document.querySelectorAll('[data-view]').forEach(button=>button.addEventListener('click',()=>view(button.dataset.view)))
  rotation.addEventListener('input',custom);zoomControl.addEventListener('input',schedule)
  document.getElementById('previewReset').addEventListener('click',reset)
  document.addEventListener('outdoor-layout-change',schedule)
  for(const key of ['width','height','depth'])document.getElementById(key).addEventListener('input',schedule)
  const observer=new ResizeObserver(schedule);observer.observe(stage)
  window.addEventListener('pagehide',event=>{cancelAnimationFrame(frame);frame=0;if(!event.persisted){disposed=true;observer.disconnect();renderer?.destroy()}})
  window.addEventListener('pageshow',schedule)
  schedule()
}
