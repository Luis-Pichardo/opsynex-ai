/// <reference types="vitest/config" />
import path from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
  },
  build: {
    rolldownOptions: {
      output: {
        // Librerías en chunks propios: cambian poco y el navegador las mantiene en caché.
        codeSplitting: {
          groups: [
            {
              name: 'react',
              test: /node_modules[\\/](react|react-dom|react-router|scheduler)[\\/]/,
            },
            {
              name: 'ui',
              test: /node_modules[\\/](@radix-ui|@floating-ui|sonner|lucide-react)[\\/]/,
            },
            {
              // Recharts y sus dependencias: solo los usan el dashboard, problemas y KPIs.
              name: 'charts',
              test: /node_modules[\\/](recharts|d3-[a-z-]+|victory-vendor|es-toolkit|immer|reselect|@reduxjs|react-redux|redux|decimal\.js-light|eventemitter3)[\\/]/,
            },
            {
              name: 'data',
              test: /node_modules[\\/](@tanstack|react-hook-form|@hookform|zod)[\\/]/,
            },
          ],
        },
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
