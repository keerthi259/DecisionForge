import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: Number(process.env['PORT'] ?? 5173),
    strictPort: true,
    proxy: {
      '/api': proxyOptions(),
      '/health': proxyOptions(),
      '/version': proxyOptions(),
    },
  },
})

function proxyOptions() {
  return {
    target: process.env['DECISIONFORGE_API_TARGET'] ?? 'http://localhost:5066',
    changeOrigin: false,
  }
}
