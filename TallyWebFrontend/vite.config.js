import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'

let outputDirectory

const serviceWorkerBuildId = {
  name: 'tally-web-service-worker-build-id',
  apply: 'build',
  configResolved(config) {
    outputDirectory = path.resolve(config.root, config.build.outDir)
  },
  closeBundle() {
    const workerPath = path.join(outputDirectory, 'sw.js')
    const worker = readFileSync(workerPath, 'utf8')
    writeFileSync(workerPath, worker.replace("'__BUILD_ID__'", JSON.stringify(new Date().toISOString())))
  },
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), serviceWorkerBuildId],
})
