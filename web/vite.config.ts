import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  // In dev the app is served from 5173 but the backend runs on 5149. Forwarding /hubs here keeps
  // the client URL relative, so the exact same code works in the container, where both are one origin.
  server: {
    proxy: {
      '/hubs': {
        target: 'http://localhost:5149',
        ws: true,
      },
    },
  },
})
