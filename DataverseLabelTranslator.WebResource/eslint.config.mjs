import js from "@eslint/js";
import globals from "globals";

const appGlobals = {
  ...globals.browser,
  $: "readonly",
  AllInOneHandler: "readonly",
  AppSettingsService: "readonly",
  AttributeHandler: "readonly",
  BpfHandler: "readonly",
  BusinessRuleHandler: "readonly",
  ChartHandler: "readonly",
  ContentSnippetHandler: "readonly",
  DataverseDataWebResourceService: "readonly",
  DialogHelper: "readonly",
  EasyTranslator: "readonly",
  EasyTranslatorHandler: "readonly",
  EntityHandler: "readonly",
  FormHandler: "readonly",
  FormMetaHandler: "readonly",
  GetGlobalContext: "readonly",
  Helper: "readonly",
  JSZip: "readonly",
  ModernCommandHandler: "readonly",
  OptionSetHandler: "readonly",
  RelationshipHandler: "readonly",
  RibbonHandler: "readonly",
  TranslationDictionaryService: "readonly",
  TranslationHandler: "readonly",
  ViewHandler: "readonly",
  WebApiClient: "readonly",
  Xrm: "readonly",
  XrmTranslator: "readonly",
  w2tabs: "readonly",
  w2utils: "readonly",
  w2alert: "readonly",
  w2confirm: "readonly",
  w2form: "readonly",
  w2grid: "readonly",
  w2popup: "readonly",
  w2toolbar: "readonly",
  w2ui: "readonly"
};

export default [
  {
    ignores: [
      "node_modules/**",
      "coverage/**",
      "release/**",
      "dist/**",
      "tmp/**",
      ".codex/**",
      ".vscode/mcp.json",
      ".agents/mcp_config.json",
      ".claude/skills/**",
      ".github/prompts/**",
      "js/lib/WebApiClient.js",
      "js/lib/w2ui.js",
      "js/lib/**"
    ]
  },
  {
    files: ["js/**/*.js"],
    languageOptions: {
      ecmaVersion: 2020,
      sourceType: "script",
      globals: appGlobals
    },
    rules: {
      ...js.configs.recommended.rules,
      "no-empty": "off",
      "no-extra-boolean-cast": "off",
      "no-prototype-builtins": "off",
      "no-redeclare": "off",
      "no-shadow-restricted-names": "off",
      "no-useless-escape": "off",
      "no-unused-vars": "off"
    }
  },
  {
    files: ["tests/**/*.js", "*.config.mjs"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        ...globals.node
      }
    },
    rules: {
      ...js.configs.recommended.rules,
      "no-unused-vars": ["warn", { argsIgnorePattern: "^_" }]
    }
  }
];
