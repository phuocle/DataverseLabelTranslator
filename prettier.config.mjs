export default {
  printWidth: 120,
  trailingComma: "none",
  endOfLine: "auto",
  overrides: [
    {
      files: "js/**/*.js",
      options: {
        tabWidth: 4
      }
    },
    {
      files: ["tests/**/*.js", "*.config.mjs"],
      options: {
        tabWidth: 2
      }
    }
  ]
};
