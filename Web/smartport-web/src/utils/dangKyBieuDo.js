import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Filler,
  Legend,
  LinearScale,
  LineElement,
  LogarithmicScale,
  PointElement,
  Tooltip,
} from 'chart.js'

// Chart.js v4 tach module: chi dang ky nhung thanh phan duoc dung de goi build nho gon
ChartJS.register(CategoryScale, LinearScale, LogarithmicScale, PointElement, LineElement, BarElement, ArcElement, Tooltip, Legend, Filler)
ChartJS.defaults.font.family = "'Segoe UI', system-ui, -apple-system, sans-serif"
ChartJS.defaults.color = '#475569'
