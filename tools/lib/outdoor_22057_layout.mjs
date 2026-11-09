export const outdoorLayoutRules = Object.freeze({ gapMm: 6, pitchOffsetMm: 115, minHeightMm: 170, minBottomHeightMm: 170, maxTotalDoors: 22, maxColumnDoors: 11 })

export const outdoorMaxColumnDoors = height => Math.min(11, Math.floor((Math.round(height * 1000) - 115000) / 176000))

export function normalizeOutdoorColumns(value, height) {
  const columns = typeof value === 'string' ? JSON.parse(value) : value
  const maximum = outdoorMaxColumnDoors(height)
  if (!Number.isFinite(height) || maximum < 1) throw new Error('请填写有效柜高')
  if (!Array.isArray(columns) || columns.length !== 2) throw new Error('请填写左右两列门板布局')
  const result = columns.map((column, index) => {
    const side = index ? 'R' : 'L'
    if (column?.side !== side || !Array.isArray(column.doors) || column.doors.length < 1 || column.doors.length > maximum) throw new Error(`当前柜高每列门数须为1–${maximum}的整数`)
    const doors = column.doors.map(door => {
      const heightMm = door?.heightMm
      if (typeof heightMm !== 'number' || !Number.isFinite(heightMm) || heightMm < 170 || Math.abs(heightMm * 1000 - Math.round(heightMm * 1000)) > 1e-6) throw new Error('每扇门高至少170 mm，最多保留三位小数')
      return { heightMm: Math.round(heightMm * 1000) / 1000 }
    })
    const used = doors.reduce((sum, door) => sum + Math.round(door.heightMm * 1000) + 6000, 0)
    if (used !== Math.round((height - 115) * 1000)) throw new Error(`${side === 'L' ? '左' : '右'}列门高与6 mm门缝未闭合，请使用自动补齐`)
    return { side, doors }
  })
  if (result.reduce((sum, column) => sum + column.doors.length, 0) > 22) throw new Error('总门数最多22门')
  return result
}

export const outdoorLayoutStyles = `.door-layout-editor [hidden]{display:none!important}.door-layout-editor{border-top:1px solid #dde3ea;margin-top:10px;padding-top:20px}.door-layout-note,.door-column-mode,.door-column-summary{font-size:12px;color:#526176}.door-columns{display:grid;grid-template-columns:1fr 1fr;gap:14px}.door-column{min-width:0;border:1px solid #dde3ea;border-radius:8px;padding:12px}.door-column legend{font-weight:600}.door-column-count input{margin-bottom:10px}.door-allocation-switch,.door-column-actions,.door-layout-presets{display:flex;gap:6px;flex-wrap:wrap;margin-bottom:10px}.door-layout-editor button{font-size:12px;padding:7px 9px;background:#edf2f7;color:#1e4d76;border:1px solid #c7d4e2;min-height:36px}.door-layout-editor button[aria-pressed=true]{background:#1e5c91;color:white}.door-height-list{list-style:none;padding:0;margin:0}.door-height-row{display:grid;grid-template-columns:26px minmax(0,1fr) auto;gap:6px;align-items:center;margin:6px 0}.door-height-row label{font-size:12px}.door-height-row label small{display:block;font-size:10px;color:#647386}.door-height-row input{margin:0;min-width:0;padding:7px}.door-height-row input[readonly]{background:#f0f4f8;color:#526176}.door-value-field small{display:block;color:#526176;font-size:11px}.door-column-summary[data-valid=false],#doorLayoutStatus[data-valid=false]{color:#a13535}.door-layout-status{font-size:13px;background:#edf5fa;border-radius:6px;padding:10px}.layout-drawing{width:100%;height:340px;background:#edf1f5;border-radius:8px}.door-layout-presets{margin-bottom:12px}@media(max-width:560px){.door-columns{grid-template-columns:1fr}}`

export function outdoorLayoutMarkup() {
  const columns = ['L', 'R'].map((side, index) => {
    const label = index ? '右' : '左', word = index ? 'right' : 'left'
    return `<fieldset class="door-column" data-door-column="${side}"><legend>${label}列 / ${side}</legend><label class="door-column-count" for="${word}DoorCount">${label}列门数<input id="${word}DoorCount" type="number" min="1" max="11" step="1" value="7" required></label><div class="door-allocation-switch" role="group" aria-label="${label}列门高设置方式"><button type="button" data-column-mode="${side}" data-allocation-mode="height" aria-pressed="true">按高度</button><button type="button" data-column-mode="${side}" data-allocation-mode="ratio" aria-pressed="false">按比例</button></div><ol id="${word}DoorHeights" class="door-height-list" aria-label="${label}列门高，从下向上"></ol><p id="${word}DoorSummary" class="door-column-summary" role="status"></p><p id="${word}DoorMode" class="door-column-mode"></p><div class="door-column-actions"><button type="button" data-column-equalize="${side}">均分本列</button><button type="button" data-column-fill-top="${side}">补齐最上门</button></div></fieldset>`
  }).join('')
  return `<section class="door-layout-editor" aria-label="左右列独立门板编辑"><h2>左右列布局</h2><p class="door-layout-note">指定已知门高，自动门补齐剩余；或按比例分格。01为最下门。室外柜每道门缝6 mm，所有门高至少170 mm；门数上限随柜高变化，2200高最多22门。</p><div class="door-layout-presets"><button type="button" data-layout-preset="standard">14门混排</button><button type="button" data-layout-preset="four">四门等高</button></div><div class="door-columns">${columns}</div><p id="doorLayoutStatus" class="door-layout-status" role="status" aria-live="polite"></p><input type="hidden" name="columns" id="doorColumnsPayload"><input type="hidden" name="doors" id="doorCountPayload" value="14"></section>`
}

function outdoorLayoutClient(enabled) {
  const form = document.getElementById('request-form'), heightInput = document.getElementById('height')
  const format = value => Number.isFinite(value) ? String(Math.round(value * 1000) / 1000) : ''
  // Same whole-thousandth allocation as the indoor editor, with outdoor datums.
  function distribute(budgetMm, weights) {
    const budget = Math.round(budgetMm * 1000), total = weights.reduce((a, b) => a + b, 0)
    if (!Number.isSafeInteger(budget) || budget < 0 || !Number.isFinite(total) || total <= 0 || weights.some(value => !Number.isFinite(value) || value <= 0)) return weights.map(() => NaN)
    const shares = weights.map(value => budget * value / total), values = shares.map(Math.floor)
    const order = shares.map((value, index) => ({ index, fraction: value - values[index] })).sort((a, b) => b.fraction - a.fraction || a.index - b.index)
    for (let i = 0, remainder = budget - values.reduce((a, b) => a + b, 0); i < remainder; i++) values[order[i % order.length].index]++
    return values.map(value => value / 1000)
  }
  const maxDoors = height => Math.min(11, Math.floor((Math.round(height * 1000) - 115000) / 176000))
  const presetHeights = (height, units) => distribute(height - 115 - 176 * units.length, units).map(value => format(value + 170))
  const seed = [3, 2, 1, 1, 1, 1, 3]
  const columns = ['L', 'R'].map((side, index) => ({ side, label: index ? '右列' : '左列',
    container: document.querySelector('[data-door-column="' + side + '"]'),
    count: document.getElementById((index ? 'right' : 'left') + 'DoorCount'), list: document.getElementById((index ? 'right' : 'left') + 'DoorHeights'),
    summary: document.getElementById((index ? 'right' : 'left') + 'DoorSummary'), modeNote: document.getElementById((index ? 'right' : 'left') + 'DoorMode'),
    mode: 'height', ratios: seed.map(String), automatic: seed.map((value, row) => row === seed.length - 1),
    heights: presetHeights(Number(heightInput.value), seed) }))
  function resolve(column, height) {
    column.error = ''
    if (column.mode === 'ratio') {
      const weights = column.ratios.map(value => value.trim() ? Number(value) : NaN)
      column.heights = distribute(height - 115, weights).map(value => format(value - 6))
      if (weights.some(value => !Number.isFinite(value) || value < .001)) column.error = '每扇门比例至少0.001。'
    } else {
      const auto = column.automatic.map((value, i) => value ? i : -1).filter(i => i >= 0)
      if (auto.length) {
        const fixed = column.heights.filter((value, i) => !column.automatic[i]).map(value => value.trim() ? Number(value) : NaN)
        const remainder = height - 115 - 6 * column.heights.length - fixed.reduce((a, b) => a + b, 0)
        const assigned = distribute(remainder, auto.map(() => 1))
        auto.forEach((index, row) => { column.heights[index] = format(assigned[row]) })
        if (fixed.some(value => !Number.isFinite(value) || value < 170) || remainder < auto.length * 170) column.error = '固定门高占用过多，或门高不足170 mm，请调整。'
      }
    }
  }
  function render(column) {
    const ratio = column.mode === 'ratio'
    if (column.list.children.length !== column.heights.length) {
      column.list.replaceChildren(...column.heights.map((value, index) => {
        const row = document.createElement('li'); row.className = 'door-height-row'
        const label = document.createElement('label'); label.textContent = String(index + 1).padStart(2, '0')
        const box = document.createElement('div'); box.className = 'door-value-field'
        const input = document.createElement('input'); input.type = 'number'; input.step = '.001'; input.required = true; input.dataset.row = String(index)
        const result = document.createElement('small'); box.append(input, result)
        const toggle = document.createElement('button'); toggle.type = 'button'; toggle.dataset.row = String(index)
        row.append(label, box, toggle); return row
      }))
    }
    column.list.querySelectorAll('input').forEach((input, index) => {
      input.id = 'door' + (ratio ? 'Ratio' : 'Height') + column.side + (index + 1); input.closest('li').querySelector('label').htmlFor = input.id
      input.setAttribute('aria-label', column.label + '第' + (index + 1) + '门' + (ratio ? '分格比例' : '成品高度（mm）'))
      input.min = ratio ? '.001' : '170'; input.readOnly = !ratio && column.automatic[index]
      const value = ratio ? column.ratios[index] : column.heights[index]; if (input.value !== value) input.value = value
      const result = input.nextElementSibling; result.hidden = !ratio; result.textContent = column.heights[index] + ' mm'
      const toggle = input.closest('li').querySelector('button'); toggle.hidden = ratio; toggle.textContent = column.automatic[index] ? '自动' : '固定'
      toggle.setAttribute('aria-label', column.label + '第' + (index + 1) + '门' + (column.automatic[index] ? '自动分配中，改为固定门高' : '固定门高中，改为自动分配'))
      toggle.setAttribute('aria-pressed', String(column.automatic[index]))
    })
    column.container.querySelectorAll('[data-column-mode]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.allocationMode === column.mode)))
    column.modeNote.textContent = ratio ? '按比例分格，下方显示已扣门缝的成品门高。' : '固定已知门高，自动门分配剩余；可点击固定 / 自动切换。'
  }
  function preview() {
    const svg = document.getElementById('layout-preview'); if (!svg) return
    const height = Number(heightInput.value), width = Number(document.getElementById('width').value)
    const ns = 'http://www.w3.org/2000/svg', children = []
    for (const [col, column] of columns.entries()) {
      let offset = 0
      column.heights.forEach((text, index) => {
        const value = Number(text); if (!Number.isFinite(value) || value <= 0) return
        const rectangle = document.createElementNS(ns, 'rect'), label = document.createElementNS(ns, 'text')
        const y = 320 - (offset + value) / height * 290, x = 105 + col * 105, h = value / height * 290
        for (const [key, value] of Object.entries({ x, y, width: 98, height: h, rx: 2, fill: '#d7e3ee', stroke: '#617d98' })) rectangle.setAttribute(key, value)
        label.setAttribute('x', x + 49); label.setAttribute('y', y + h / 2 + 4); label.setAttribute('text-anchor', 'middle'); label.setAttribute('font-size', '11')
        label.textContent = column.side + (index + 1) + ' · ' + format(value); children.push(rectangle, label); offset += value + 6
      })
    }
    const caption = document.createElementNS(ns, 'text'); caption.setAttribute('x', '210'); caption.setAttribute('y', '18'); caption.setAttribute('text-anchor', 'middle'); caption.setAttribute('font-size', '12'); caption.textContent = width + '宽 × ' + height + '高 mm · 布局预览'; children.push(caption)
    svg.replaceChildren(...children)
  }
  function update() {
    const height = Number(heightInput.value), errors = [], data = []
    for (const column of columns) {
      resolve(column, height); render(column)
      const heights = column.heights.map(value => value.trim() ? Number(value) : NaN), count = Number(column.count.value)
      const remaining = Math.round((height - 115 - 6 * heights.length) * 1000) - heights.reduce((sum, value) => sum + Math.round(value * 1000), 0)
      column.count.max = maxDoors(height)
      if (heights.some(value => value < 170)) column.error = '所有门高至少170 mm，请调整。'
      if (count > maxDoors(height)) column.error = '当前柜高每列最多' + maxDoors(height) + '门。'
      const valid = Number.isInteger(count) && count >= 1 && count <= maxDoors(Number(heightInput.value)) && count === heights.length && !column.error && heights.every(value => Number.isFinite(value) && value >= 170) && remaining === 0 && Array.from(column.list.querySelectorAll('input')).every(input => input.checkValidity())
      column.summary.dataset.valid = String(valid)
      column.summary.textContent = valid ? '门高合计 ' + format(heights.reduce((a, b) => a + b, 0)) + ' mm · 已闭合' : column.error || (Number.isFinite(remaining) && remaining !== 0 ? '本列' + (remaining > 0 ? '还差' : '超出') + format(Math.abs(remaining) / 1000) + ' mm，请自动补齐。' : '请检查门数和门高。')
      if (!valid) errors.push(column.label + '：' + column.summary.textContent)
      data.push({ side: column.side, doors: heights.map(heightMm => ({ heightMm })) })
    }
    const total = data.reduce((sum, column) => sum + column.doors.length, 0); if (total > 22) errors.push('总门数最多22门。')
    const valid = !errors.length && heightInput.checkValidity(), status = document.getElementById('doorLayoutStatus')
    status.dataset.valid = String(valid); status.textContent = valid ? '左' + data[0].doors.length + '门 / 右' + data[1].doors.length + '门，共' + total + '门，两列均已闭合。' : errors.join(' ') || '请检查柜高。'
    form.dataset.layoutValid = String(valid); document.getElementById('doorColumnsPayload').value = JSON.stringify(data); document.getElementById('doorCountPayload').value = total
    document.getElementById('submit').disabled = !enabled || !valid; preview()
  }
  for (const column of columns) {
    column.count.addEventListener('input', () => { const count = Number(column.count.value); if (Number.isInteger(count) && count >= 1 && count <= maxDoors(Number(heightInput.value)) && count !== column.heights.length) { column.ratios = Array(count).fill('1'); column.automatic = Array(count).fill(true); column.heights = Array(count).fill('') } update() })
    column.list.addEventListener('input', event => { const input = event.target; if (!input.matches('input')) return; const i = Number(input.dataset.row); if (column.mode === 'ratio') column.ratios[i] = input.value; else { column.automatic[i] = false; column.heights[i] = input.value } update() })
    column.list.addEventListener('click', event => { const button = event.target.closest('button'); if (!button) return; const i = Number(button.dataset.row); column.automatic[i] = !column.automatic[i]; update(); if (!column.automatic[i]) column.list.querySelectorAll('input')[i].focus() })
    column.container.querySelectorAll('[data-column-mode]').forEach(button => button.addEventListener('click', () => { const mode = button.dataset.allocationMode; if (mode === 'ratio') column.ratios = column.heights.map(value => format(Number(value) + 6)); else column.automatic = column.heights.map((value, i) => i === column.heights.length - 1); column.mode = mode; update() }))
    column.container.querySelector('[data-column-equalize]').addEventListener('click', () => { column.ratios = column.heights.map(() => '1'); column.automatic = column.heights.map(() => true); update() })
    column.container.querySelector('[data-column-fill-top]').addEventListener('click', () => { column.mode = 'height'; column.automatic = column.heights.map((value, i) => i === column.heights.length - 1); update() })
  }
  document.querySelectorAll('[data-layout-preset]').forEach(button => button.addEventListener('click', () => { const units = button.dataset.layoutPreset === 'four' ? [1, 1] : seed; for (const column of columns) { column.mode = 'height'; column.count.value = units.length; column.ratios = units.map(String); column.automatic = units.map((value, i) => i === units.length - 1); column.heights = presetHeights(Number(heightInput.value), units) } update() }))
  heightInput.addEventListener('input', update); document.getElementById('width').addEventListener('input', preview); update()
}

export const outdoorLayoutScript = enabled => `(${outdoorLayoutClient.toString()})(${Boolean(enabled)});`
