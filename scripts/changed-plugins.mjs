#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = process.cwd();
const catalog = JSON.parse(fs.readFileSync(path.join(root, "plugins/catalog.json"), "utf8"));

function changedFiles() {
  const event = process.env.GITHUB_EVENT_NAME || "";
  const before = process.env.GITHUB_EVENT_BEFORE || "";
  try {
    if (event === "pull_request") {
      const base = process.env.GITHUB_BASE_SHA || "origin/main";
      const head = process.env.GITHUB_SHA || "HEAD";
      return execSync(`git diff --name-only ${base}...${head}`, { encoding: "utf8" })
        .split("\n")
        .filter(Boolean);
    }
    if (event === "push" && before && !/^0+$/.test(before)) {
      return execSync(`git diff --name-only ${before} ${process.env.GITHUB_SHA}`, { encoding: "utf8" })
        .split("\n")
        .filter(Boolean);
    }
  } catch {
    // Treat as all plugins when git history is unavailable.
  }
  return null;
}

function pluginForFile(file) {
  const normalized = file.replaceAll("\\", "/");
  return catalog.extensions.find((entry) => {
    const pluginRoot = `plugins/${entry.slug}`;
    return normalized === entry.project
      || normalized === entry.manifest
      || normalized === entry.path
      || normalized.startsWith(`${pluginRoot}/`);
  });
}

function matchesFilter(entry, requested) {
  if (!requested || requested === "all") return true;
  return entry.id === requested
    || entry.slug === requested
    || entry.name === requested
    || requested.startsWith(entry.tagPrefix);
}

const files = process.env.FORCE_ALL_PLUGINS === "true" ? null : changedFiles();
let selected = catalog.extensions;
if (files) {
  const shared = files.some((file) =>
    file === "Directory.Build.props"
    || file === "global.json"
    || file === "plugins/catalog.json"
    || file.startsWith("scripts/")
    || file.startsWith(".github/workflows/"));
  if (!shared) {
    const ids = new Set();
    for (const file of files) {
      const plugin = pluginForFile(file);
      if (plugin) ids.add(plugin.id);
    }
    selected = catalog.extensions.filter((entry) => ids.has(entry.id));
  }
}

selected = selected.filter((entry) => matchesFilter(entry, process.env.PLUGIN_FILTER?.trim()));

if (selected.length === 0) {
  console.log("No plugins changed.");
}

const matrix = {
  include: selected.map((entry) => {
    const manifest = JSON.parse(fs.readFileSync(path.join(root, entry.manifest), "utf8"));
    return {
      name: entry.name,
      id: entry.id,
      slug: entry.slug,
      path: entry.path,
      project: entry.project,
      manifest: entry.manifest,
      tagPrefix: entry.tagPrefix,
      version: manifest.version,
    };
  }),
};

const encoded = JSON.stringify(matrix);
const githubOutput = process.env.GITHUB_OUTPUT;
if (githubOutput) {
  fs.appendFileSync(githubOutput, `matrix=${encoded}\n`);
  fs.appendFileSync(githubOutput, `count=${selected.length}\n`);
}
console.log(encoded);
