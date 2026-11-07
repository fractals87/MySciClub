import { readFileSync } from 'node:fs'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig(({ mode }) => {
  const pfxPath = process.env.MYSCICLUB_DEV_HTTPS_PFX_PATH
  const pfxPassword = process.env.MYSCICLUB_DEV_HTTPS_PFX_PASSWORD
  const apiTarget = process.env.MYSCICLUB_API_PROXY_TARGET ?? 'https://localhost:7289'

  if (mode === 'development' && (!pfxPath || !pfxPassword)) {
    throw new Error(
      'HTTPS development requires MYSCICLUB_DEV_HTTPS_PFX_PATH and MYSCICLUB_DEV_HTTPS_PFX_PASSWORD.',
    )
  }

  if (mode === 'development' && !apiTarget.startsWith('https://')) {
    throw new Error('MYSCICLUB_API_PROXY_TARGET must use HTTPS.')
  }

  return {
    plugins: [react()],
    server: {
      host: 'localhost',
      ...(mode === 'development' && pfxPath && pfxPassword
        ? { https: { pfx: readFileSync(pfxPath), passphrase: pfxPassword } }
        : {}),
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: false,
          secure: true,
        },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      passWithNoTests: false,
    },
  }
})
