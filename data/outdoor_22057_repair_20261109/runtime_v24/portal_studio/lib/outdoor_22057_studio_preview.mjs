// Layout visualization only. Native geometry is generated and accepted separately.
export function outdoorStudioPreview() {
  const canvas = document.getElementById('cabinetPreview'), ctx = canvas.getContext('2d')
  const rotation = document.getElementById('previewRotation'), zoom = document.getElementById('previewZoom')
  let tilt = 12, selectedView = 'iso', dragging = null
  const number = (id, fallback) => { const value = Number(document.getElementById(id).value); return Number.isFinite(value) && value > 0 ? value : fallback }
  const colors = { front: '#eef1f4', right: '#c9d2dd', left: '#c9d2dd', top: '#fafbfc', bottom: '#a8b5c5', back: '#c2cdd9' }
  function rotate(point) {
    const angle = Number(rotation.value) * Math.PI / 180, elevation = tilt * Math.PI / 180
    const x = point.x * Math.cos(angle) - point.z * Math.sin(angle), z = point.x * Math.sin(angle) + point.z * Math.cos(angle)
    return { x, y: point.y * Math.cos(elevation) - z * Math.sin(elevation), z: point.y * Math.sin(elevation) + z * Math.cos(elevation) }
  }
  function box(faces, x, y, z, w, h, d, palette = colors) {
    const a = x - w / 2, b = x + w / 2, c = y - h / 2, e = y + h / 2, f = z - d / 2, g = z + d / 2
    const point = (x, y, z) => ({ x, y, z })
    for (const [normal, points, color] of [
      [[0, 0, 1], [[a,c,g],[b,c,g],[b,e,g],[a,e,g]], palette.front],
      [[1, 0, 0], [[b,c,g],[b,c,f],[b,e,f],[b,e,g]], palette.right],
      [[-1, 0, 0], [[a,c,f],[a,c,g],[a,e,g],[a,e,f]], palette.left],
      [[0, 1, 0], [[a,e,g],[b,e,g],[b,e,f],[a,e,f]], palette.top],
      [[0, -1, 0], [[a,c,f],[b,c,f],[b,c,g],[a,c,g]], palette.bottom],
      [[0, 0, -1], [[b,c,f],[a,c,f],[a,e,f],[b,e,f]], palette.back || palette.right]
    ]) faces.push({ normal: point(...normal), points: points.map(p => point(...p)), color })
  }
  function draw() {
    const W = number('width', 950), H = number('height', 1939), D = number('depth', 550)
    let columns = []
    try { columns = JSON.parse(document.getElementById('doorColumnsPayload').value) } catch {}
    const faces = [], bodyH = H - 95, bodyY = -47.5
    box(faces, 0, bodyY, 0, W, bodyH, D)
    box(faces, 0, -H / 2 + 30, 0, W - 12, 60, D - 8, { ...colors, front:'#cbd5e1', right:'#aebdce' })
    const doorW = W / 2 - 26, bottom = -H / 2 + 58
    columns.forEach((column, col) => {
      let y = bottom
      for (let index = 0; index < column.doors.length; index++) {
        const height = column.doors[index].heightMm
        if (!Number.isFinite(height) || height < 1) continue
        const x = (col ? 1 : -1) * (W / 4 - 1.5)
        box(faces, x, y + height / 2, D / 2 + 6, doorW, height, 12, { ...colors, front: index % 2 ? '#eef2f7' : '#f6f8fb' })
        // The latch symbol identifies the door; it is not a hardware model.
        box(faces, x + (col ? -1 : 1) * (doorW / 2 - 25), y + height / 2, D / 2 + 14, 5, 18, 3, { ...colors, front:'#66788e', right:'#52647a' })
        y += height + 6
      }
    })
    const roofY = H / 2 - 6
    for (const x of [-1, 1]) box(faces, x * (W / 2 - 52), roofY - 58, 0, 26, 116, 42, { ...colors, front:'#b3bfce', right:'#9aaabd' })
    box(faces, 0, roofY, 45, Math.max(150, W - 160), 18, D + 230, { ...colors, front:'#a2b3c7', right:'#a9b9cd', top:'#ccd6e3' })
    box(faces, 0, roofY - 18, (D + 230) / 2 + 30, Math.max(120, W - 222), 24, 30, { ...colors, front:'#ecf0f5', right:'#adbacb' })
    const transformed = faces.map(face => ({ ...face, normal: rotate(face.normal), points: face.points.map(rotate) })).filter(face => face.normal.z > 0.00001)
    const all = transformed.flatMap(face => face.points)
    const minX = Math.min(...all.map(p => p.x)), maxX = Math.max(...all.map(p => p.x)), minY = Math.min(...all.map(p => p.y)), maxY = Math.max(...all.map(p => p.y))
    const scale = Math.min(570 / (maxX - minX), 530 / (maxY - minY)) * Number(zoom.value) / 100
    const project = p => ({ x: canvas.width / 2 + (p.x - (minX + maxX) / 2) * scale, y: canvas.height / 2 + 12 - (p.y - (minY + maxY) / 2) * scale })
    ctx.clearRect(0, 0, canvas.width, canvas.height)
    const background = ctx.createLinearGradient(0, 0, 0, canvas.height); background.addColorStop(0, '#f8f9fb'); background.addColorStop(1, '#e9edf3')
    ctx.fillStyle = background; ctx.fillRect(0, 0, canvas.width, canvas.height)
    ctx.strokeStyle = '#dfe5ee'; ctx.lineWidth = 0.6; ctx.beginPath()
    for (let x = 30; x < canvas.width; x += 40) { ctx.moveTo(x, 0); ctx.lineTo(x, canvas.height) }
    for (let y = 30; y < canvas.height; y += 40) { ctx.moveTo(0, y); ctx.lineTo(canvas.width, y) }
    ctx.stroke()
    if (selectedView === 'iso') { ctx.save(); ctx.fillStyle = '#8092aa23'; ctx.filter = 'blur(14px)'; ctx.beginPath(); ctx.ellipse(450, 635, Math.min(180, W * scale * .65), 22, 0, 0, Math.PI * 2); ctx.fill(); ctx.restore() }
    transformed.sort((a, b) => a.points.reduce((s,p)=>s+p.z,0) / a.points.length - b.points.reduce((s,p)=>s+p.z,0) / b.points.length)
    for (const face of transformed) {
      const points = face.points.map(project); ctx.beginPath(); points.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); ctx.closePath()
      ctx.fillStyle = face.color; ctx.fill(); ctx.lineWidth = .7; ctx.strokeStyle = '#8fa0b5'; ctx.stroke()
    }
    function dimension(a, b, label, offsetX = 0, offsetY = 0) {
      const p = project(rotate(a)), q = project(rotate(b)), start = {x:p.x+offsetX,y:p.y+offsetY}, end = {x:q.x+offsetX,y:q.y+offsetY}
      ctx.save(); ctx.strokeStyle='#9cabbc';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(start.x,start.y);ctx.lineTo(end.x,end.y);ctx.lineTo(q.x,q.y);ctx.stroke()
      const angle=Math.atan2(end.y-start.y,end.x-start.x)
      for (const point of [start,end]) {ctx.beginPath();ctx.moveTo(point.x-5*Math.cos(angle+.7),point.y-5*Math.sin(angle+.7));ctx.lineTo(point.x+5*Math.cos(angle+.7),point.y+5*Math.sin(angle+.7));ctx.stroke()}
      const x=(start.x+end.x)/2,y=(start.y+end.y)/2;ctx.font='13px "Segoe UI", "Microsoft YaHei"';const textWidth=ctx.measureText(label).width
      ctx.fillStyle='#f1f4f8';ctx.fillRect(x-textWidth/2-5,y-10,textWidth+10,20);ctx.fillStyle='#60758f';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(label,x,y);ctx.restore()
    }
    if (Number(zoom.value) <= 110 && (selectedView === 'iso' || selectedView === 'front')) {
      dimension({x:-W/2,y:-H/2,z:D/2+20},{x:W/2,y:-H/2,z:D/2+20},'W '+W,0,24)
      dimension({x:W/2,y:-H/2,z:D/2+20},{x:W/2,y:H/2,z:D/2+20},'H '+H,34,0)
    }
    ctx.save();ctx.font='11px "Segoe UI"';const origin={x:46,y:canvas.height-52}
    for(const [label,point,color] of [['X',{x:32,y:0,z:0},'#a77c7c'],['Y',{x:0,y:32,z:0},'#7a9e91'],['Z',{x:0,y:0,z:32},'#7b93b4']]){const p=rotate(point);ctx.strokeStyle=color;ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(origin.x,origin.y);ctx.lineTo(origin.x+p.x,origin.y-p.y);ctx.stroke();ctx.fillText(label,origin.x+p.x+3,origin.y-p.y-3)}ctx.restore()
    if (selectedView === 'front') {
      columns.forEach((column, col) => {
        let y = bottom
        column.doors.forEach((door, index) => {
          if (!Number.isFinite(door.heightMm) || door.heightMm < 1) return
          const p = project(rotate({ x: (col ? 1 : -1) * (W / 4 - 1.5), y: y + door.heightMm / 2, z: D / 2 + 16 }))
          ctx.font = '13px "Segoe UI", "Microsoft YaHei"'; ctx.fillStyle = '#687d99'; ctx.textAlign = 'center'; ctx.fillText(column.side + String(index + 1).padStart(2,'0') + ' · ' + door.heightMm, p.x, p.y + 4); y += door.heightMm + 6
        })
      })
    }
    ctx.font = '12px "Segoe UI", "Microsoft YaHei"'; ctx.fillStyle = '#8392a6'; ctx.textAlign = 'center'; ctx.fillText(selectedView === 'top' ? W + ' W × ' + D + ' D / mm' : W + ' W × ' + H + ' H × ' + D + ' D / mm', 450, 698)
    document.getElementById('rotationLabel').textContent = rotation.value + '°'; document.getElementById('zoomLabel').textContent = zoom.value + '%'
    document.getElementById('hudCabinetSize').textContent = W + ' × ' + H + ' × ' + D + ' mm'
    document.getElementById('hudDoorLayout').textContent = '左 ' + (columns[0]?.doors.length || 0) + ' / 右 ' + (columns[1]?.doors.length || 0) + ' · 共 ' + columns.reduce((sum, c) => sum + c.doors.length, 0) + ' 门'
    document.getElementById('hudDoorSize').textContent = doorW + ' mm'
    document.getElementById('hudLayoutState').textContent = document.getElementById('request-form').dataset.layoutValid === 'true' ? '两列均已闭合 · 门高 ≥170 mm' : '布局待调整'
    canvas.dataset.renderVersion = String(Number(canvas.dataset.renderVersion || 0) + 1)
  }
  function view(name) {
    selectedView = name
    const values = { iso: [-28, 12], front: [0, 0], right: [-90, 0], top: [0, 90] }[name]
    rotation.value = values[0]; tilt = values[1]
    document.querySelectorAll('[data-view]').forEach(button => { button.classList.toggle('view-active', button.dataset.view === name); button.setAttribute('aria-pressed', String(button.dataset.view === name)) })
    draw()
  }
  document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => view(button.dataset.view)))
  rotation.addEventListener('input', () => { selectedView = 'custom'; if (Math.abs(tilt) > 89) tilt = 12; document.querySelectorAll('[data-view]').forEach(button => { button.classList.remove('view-active'); button.setAttribute('aria-pressed', 'false') }); draw() })
  zoom.addEventListener('input', draw)
  canvas.addEventListener('pointerdown', event => { dragging = { x: event.clientX, rotation: Number(rotation.value) }; canvas.setPointerCapture(event.pointerId) })
  canvas.addEventListener('pointermove', event => { if (!dragging) return; rotation.value = Math.max(-90, Math.min(90, dragging.rotation + (event.clientX - dragging.x) / 4)); rotation.dispatchEvent(new Event('input')) })
  for (const name of ['pointerup', 'pointercancel', 'lostpointercapture']) canvas.addEventListener(name, () => { dragging = null })
  canvas.addEventListener('wheel', event => { event.preventDefault(); zoom.value = Math.max(70, Math.min(150, Number(zoom.value) + (event.deltaY > 0 ? -5 : 5))); draw() }, { passive: false })
  document.addEventListener('outdoor-layout-change', draw)
  for (const key of ['width', 'height', 'depth']) document.getElementById(key).addEventListener('input', draw)
  draw()
}
