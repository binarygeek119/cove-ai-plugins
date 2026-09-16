#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const catalog = JSON.parse(fs.readFileSync(path.join(root, "plugins/catalog.json"), "utf8"));

const skipDirs = new Set(["bin", "obj", "artifacts", "node_modules", ".git"]);
const textExts = new Set([".cs", ".mjs", ".js", ".ts", ".json", ".yml", ".yaml", ".md", ".props", ".csproj"]);

const findings = [];

const csharpRules = [
  {
    id: "process-start",
    severity: "high",
    pattern: /\bProcess\.Start\b|\bProcessStartInfo\b/,
    message: "Starting OS processes can run untrusted commands.",
  },
  {
    id: "native-import",
    severity: "high",
    pattern: /\[DllImport\b|\[LibraryImport\b/,
    message: "Native imports bypass the managed sandbox.",
  },
  {
    id: "assembly-load",
    severity: "high",
    pattern: /\bAssembly\.Load(From|File)?\s*\(/,
    message: "Loading assemblies at runtime can execute untrusted code.",
  },
  {
    id: "reflection-emit",
    severity: "medium",
    pattern: /\bSystem\.Reflection\.Emit\b|\bAssemblyBuilder\b/,
    message: "Runtime code generation is unusual for a Cove plugin.",
  },
  {
    id: "unrestricted-socket",
    severity: "medium",
    pattern: /\bTcpClient\b|\bSocket\s*\(|\bUdpClient\b|\bHttpListener\b/,
    message: "Raw sockets can ignore the extension network allowlist.",
  },
  {
    id: "registry-write",
    severity: "high",
    pattern: /\bMicrosoft\.Win32\.Registry\b|\bRegistryKey\b/,
    message: "Windows registry access is not needed for Cove plugins.",
  },
];

const jsRules = [
  {
    id: "eval",
    severity: "high",
    pattern: /\beval\s*\(|\bnew Function\s*\(/,
    message: "Dynamic JS evaluation can run untrusted code.",
  },
  {
    id: "child-process",
    severity: "high",
    pattern: /\bchild_process\b|\bprocess\.binding\b/,
    message: "Node child processes are not available in Cove UI bundles and are a red flag.",
  },
];

const secretRules = [
  {
    id: "private-key",
    severity: "high",
    pattern: /-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----/,
    message: "Private key material must not be committed.",
  },
  {
    id: "hardcoded-token",
    severity: "high",
    pattern: /\b(?:sk|rk|ghp|github_pat|xox[baprs])-[A-Za-z0-9_-]{16,}\b/,
    message: "Looks like a hardcoded API token.",
  },
];

function walk(dir, files = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (skipDirs.has(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, files);
    else files.push(full);
  }
  return files;
}

function addFinding(file, line, rule, excerpt) {
  findings.push({
    file: path.relative(root, file).replaceAll("\\", "/"),
    line,
    severity: rule.severity,
    id: rule.id,
    message: rule.message,
    excerpt: excerpt.trim().slice(0, 200),
  });
}

function scanFile(file, rules) {
  const text = fs.readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);
  lines.forEach((line, index) => {
    for (const rule of rules) {
      if (rule.pattern.test(line)) addFinding(file, index + 1, rule, line);
    }
  });
}

function scanManifest(manifestPath) {
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  const network = manifest.permissions?.network;
  if (network === "*" || (Array.isArray(network) && network.includes("*"))) {
    addFinding(manifestPath, 1, {
      id: "open-network",
      severity: "high",
      message: "Network permission '*' allows the plugin to call any host.",
    }, JSON.stringify(network));
  }
  if (!manifest.id || !/^[a-z0-9]+(\.[a-z0-9-]+)+$/.test(manifest.id)) {
    addFinding(manifestPath, 1, {
      id: "invalid-id",
      severity: "medium",
      message: "Extension id should be reverse-domain lowercase (com.example.name).",
    }, String(manifest.id ?? ""));
  }
  if (!manifest.minCoveVersion) {
    addFinding(manifestPath, 1, {
      id: "missing-min-cove",
      severity: "medium",
      message: "Set minCoveVersion so older Cove hosts refuse incompatible plugins.",
    }, "");
  }
}

for (const plugin of catalog.extensions) {
  const pluginRoot = path.join(root, "plugins", plugin.slug);
  const files = walk(pluginRoot);
  for (const file of files) {
    const ext = path.extname(file);
    if (!textExts.has(ext)) continue;
    if (ext === ".cs") scanFile(file, [...csharpRules, ...secretRules]);
    else if (ext === ".mjs" || ext === ".js" || ext === ".ts") scanFile(file, [...jsRules, ...secretRules]);
    else scanFile(file, secretRules);
  }
  scanManifest(path.join(root, plugin.manifest));
}

const report = path.join(root, "artifacts/security/plugin-safety.json");
fs.mkdirSync(path.dirname(report), { recursive: true });
fs.writeFileSync(report, JSON.stringify({ findings }, null, 2) + "\n");

if (findings.length === 0) {
  console.log("Plugin safety scan passed. No issues found.");
  process.exit(0);
}

const highs = findings.filter((item) => item.severity === "high");
for (const item of findings) {
  console.log(`${item.severity.toUpperCase()} ${item.file}:${item.line} [${item.id}] ${item.message}`);
  if (item.excerpt) console.log(`  ${item.excerpt}`);
}

if (highs.length > 0) {
  console.error(`\n${highs.length} high-severity safety issue(s).`);
  process.exit(1);
}

console.log(`\n${findings.length} non-blocking finding(s).`);
