import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/**/*.test.js"],
    passWithNoTests: true,
    setupFiles: ["tests/setup.js"],
    coverage: {
      provider: "v8",
      include: ["js/**/*.js"],
      exclude: ["js/lib/**"],
      reporter: ["text", "json", "json-summary", "html"],
      thresholds: {
        lines: 60,
        functions: 60,
        branches: 60,
        statements: 60
      }
    }
  }
});
