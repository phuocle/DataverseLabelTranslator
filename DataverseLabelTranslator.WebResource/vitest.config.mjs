import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/**/*.test.js"],
    coverage: {
      provider: "v8",
      include: [
        "js/Handler/DashboardHandler.js",
        "js/Handler/GlobalOptionSetHandler.js",
        "js/Handler/WebResourceHandler.js"
      ],
      reporter: ["text", "json", "json-summary", "html"],
      thresholds: {
        lines: 100,
        functions: 100,
        branches: 100,
        statements: 100
      }
    }
  }
});
