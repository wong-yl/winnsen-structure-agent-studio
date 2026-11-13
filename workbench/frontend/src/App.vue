<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'

const API_BASE = 'http://localhost:8000/api'

const projectInfo = ref(null)
const statistics = ref(null)
const parts = ref([])
const loading = ref(true)

onMounted(async () => {
  try {
    // 获取项目信息
    const infoRes = await axios.get(`${API_BASE}/project/info`)
    projectInfo.value = infoRes.data.data

    // 获取统计数据
    const statsRes = await axios.get(`${API_BASE}/statistics/summary`)
    statistics.value = statsRes.data.data

    // 获取零件列表
    const partsRes = await axios.get(`${API_BASE}/parts`)
    parts.value = partsRes.data.data

    loading.value = false
  } catch (error) {
    console.error('加载数据失败:', error)
    loading.value = false
  }
})

const getCategoryColor = (category) => {
  const colors = {
    '门板': 'bg-blue-500',
    '门框': 'bg-cyan-500',
    '门配件': 'bg-teal-500',
    '箱体': 'bg-amber-500',
    '上盖': 'bg-orange-500',
    '底座': 'bg-red-500'
  }
  return colors[category] || 'bg-gray-500'
}
</script>

<template>
  <div class="min-h-screen bg-gradient-to-br from-[#05070C] via-[#0A0D12] to-[#0F131C]">
    <!-- 头部 -->
    <header class="border-b border-[#1E2636] bg-[#0A0D12]/80 backdrop-blur-sm sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-6 py-4">
        <div class="flex items-center justify-between">
          <div>
            <h1 class="text-2xl font-bold text-[#38BDF8]">Winnsen订单工作台</h1>
            <p v-if="projectInfo" class="text-sm text-[#94A3B8] mt-1">
              订单号: {{ projectInfo.project.id }} | {{ projectInfo.project.name }}
            </p>
          </div>
          <div class="text-right">
            <div class="text-sm text-[#94A3B8]">规格尺寸</div>
            <div v-if="projectInfo" class="text-lg font-semibold">
              {{ projectInfo.project.specs.width_mm }}×{{ projectInfo.project.specs.height_mm }}×{{ projectInfo.project.specs.depth_mm }}mm
            </div>
          </div>
        </div>
      </div>
    </header>

    <!-- 主内容 -->
    <main class="max-w-7xl mx-auto px-6 py-8">
      <div v-if="loading" class="text-center py-20">
        <div class="inline-block animate-spin rounded-full h-12 w-12 border-b-2 border-[#38BDF8]"></div>
        <p class="mt-4 text-[#94A3B8]">加载中...</p>
      </div>

      <div v-else>
        <!-- 统计卡片 -->
        <div v-if="statistics" class="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <div class="bg-[#0F131C] rounded-3xl p-6 border border-[#1E2636]">
            <div class="text-[#94A3B8] text-sm mb-2">工程图文件</div>
            <div class="text-3xl font-bold text-[#38BDF8]">{{ statistics.files.slddrw }}</div>
            <div class="mt-2 text-xs text-[#6EE7B7]">✓ {{ statistics.completion.drawings }}% 完成</div>
          </div>

          <div class="bg-[#0F131C] rounded-3xl p-6 border border-[#1E2636]">
            <div class="text-[#94A3B8] text-sm mb-2">PDF图纸</div>
            <div class="text-3xl font-bold text-[#38BDF8]">{{ statistics.files.pdf }}</div>
            <div class="mt-2 text-xs text-[#6EE7B7]">✓ {{ statistics.completion.pdf }}% 完成</div>
          </div>

          <div class="bg-[#0F131C] rounded-3xl p-6 border border-[#1E2636]">
            <div class="text-[#94A3B8] text-sm mb-2">展开图DXF</div>
            <div class="text-3xl font-bold text-[#38BDF8]">{{ statistics.files.dxf }}</div>
            <div class="mt-2 text-xs" :class="statistics.files.dxf > 0 ? 'text-[#6EE7B7]' : 'text-[#E9A568]'">
              {{ statistics.files.dxf > 0 ? '✓' : '⚠' }} {{ statistics.completion.dxf }}% 完成
            </div>
          </div>

          <div class="bg-[#0F131C] rounded-3xl p-6 border border-[#1E2636]">
            <div class="text-[#94A3B8] text-sm mb-2">零件总数</div>
            <div class="text-3xl font-bold text-[#38BDF8]">{{ parts.length }}</div>
            <div class="mt-2 text-xs text-[#94A3B8]">{{ Object.keys(statistics.by_category).length }} 个部件类别</div>
          </div>
        </div>

        <!-- 部件分类统计 -->
        <div v-if="statistics" class="bg-[#0F131C] rounded-3xl p-6 border border-[#1E2636] mb-8">
          <h2 class="text-lg font-semibold mb-4">部件分类统计</h2>
          <div class="grid grid-cols-2 md:grid-cols-6 gap-4">
            <div v-for="(count, category) in statistics.by_category" :key="category"
                 class="text-center p-4 rounded-2xl bg-[#161D2B]">
              <div :class="getCategoryColor(category)"
                   class="w-12 h-12 rounded-full mx-auto mb-2 flex items-center justify-center text-white font-bold">
                {{ count }}
              </div>
              <div class="text-sm text-[#F8FAFC]">{{ category }}</div>
            </div>
          </div>
        </div>

        <!-- 零件列表 -->
        <div class="bg-[#0F131C] rounded-3xl p-6 border border-[#1E2636]">
          <h2 class="text-lg font-semibold mb-4">零件清单</h2>
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead>
                <tr class="border-b border-[#1E2636] text-left text-sm text-[#94A3B8]">
                  <th class="pb-3 font-medium">零件图号</th>
                  <th class="pb-3 font-medium">零件名称</th>
                  <th class="pb-3 font-medium">部件类别</th>
                  <th class="pb-3 font-medium">材料</th>
                  <th class="pb-3 font-medium">批次号</th>
                  <th class="pb-3 font-medium">状态</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="part in parts" :key="part.零件图号"
                    class="border-b border-[#161D2B] hover:bg-[#161D2B] transition-colors">
                  <td class="py-3 text-sm font-mono text-[#38BDF8]">{{ part.零件图号 }}</td>
                  <td class="py-3 text-sm">{{ part.零件名称 }}</td>
                  <td class="py-3">
                    <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium"
                          :class="getCategoryColor(part.部件类别) + ' text-white'">
                      {{ part.部件类别 }}
                    </span>
                  </td>
                  <td class="py-3 text-sm text-[#94A3B8]">{{ part.材料规格 }}</td>
                  <td class="py-3 text-sm font-mono text-[#E9A568]">{{ part.批次号 }}</td>
                  <td class="py-3">
                    <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-[#6EE7B7] text-[#0A0D12]">
                      {{ part.状态 }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </main>
  </div>
</template>
