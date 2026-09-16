import { createElement as h, useEffect, useState } from "@cove/runtime/react";
import { extensionFetch } from "@cove/runtime/api";

const EXTENSION_ID = "com.binarygeek119.media-covers";
const CONFIG_URL = `/api/plugins/${encodeURIComponent(EXTENSION_ID)}/config`;
const RUN_URL = `/api/extensions/${encodeURIComponent(EXTENSION_ID)}/jobs/generate-missing-covers/run`;

const defaults = {
  includeAudio: true,
  includeText: true,
  maxItems: "",
  maxTextCharacters: "2000",
};

function asString(value, fallback = "") {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return fallback;
}

function asBool(value, fallback) {
  if (typeof value === "boolean") return value;
  if (typeof value === "string") {
    if (value.toLowerCase() === "true") return true;
    if (value.toLowerCase() === "false") return false;
  }
  return fallback;
}

function fromConfig(raw) {
  return {
    includeAudio: asBool(raw?.includeAudio, defaults.includeAudio),
    includeText: asBool(raw?.includeText, defaults.includeText),
    maxItems: asString(raw?.maxItems, defaults.maxItems),
    maxTextCharacters: asString(raw?.maxTextCharacters, defaults.maxTextCharacters) || defaults.maxTextCharacters,
  };
}

async function readJson(response) {
  const body = await response.text().catch(() => "");
  if (!response.ok) {
    throw new Error(body || `Request failed (${response.status})`);
  }
  if (!body.trim()) return undefined;
  return JSON.parse(body);
}

function Field({ label, description, children }) {
  return h("label", { className: "block space-y-1" },
    h("span", { className: "text-sm font-medium" }, label),
    description ? h("span", { className: "block text-xs text-secondary" }, description) : null,
    children,
  );
}

function inputClass() {
  return "w-full bg-card border border-border rounded px-2 py-1 text-sm focus:border-accent outline-none";
}

function Toggle({ label, description, value, onChange }) {
  return h("div", { className: "flex items-start justify-between gap-4" },
    h("div", null,
      h("div", { className: "text-sm font-medium" }, label),
      h("div", { className: "text-xs text-secondary" }, description),
    ),
    h("button", {
      type: "button",
      onClick: () => onChange(!value),
      className: value
        ? "px-3 py-1 text-xs rounded font-medium transition-colors bg-green-600/20 text-green-400 hover:bg-green-600/30"
        : "px-3 py-1 text-xs rounded font-medium transition-colors bg-card/30 text-secondary hover:bg-card-hover/40",
    }, value ? "On" : "Off"),
  );
}

function SettingsPanel() {
  const [values, setValues] = useState(defaults);
  const [saved, setSaved] = useState(defaults);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState(null);
  const [status, setStatus] = useState(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await extensionFetch(CONFIG_URL);
        const loaded = fromConfig(await readJson(response));
        if (cancelled) return;
        setValues(loaded);
        setSaved(loaded);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const dirty = JSON.stringify(values) !== JSON.stringify(saved);
  const update = (key, value) => {
    setValues((current) => ({ ...current, [key]: value }));
    setStatus(null);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    setStatus(null);
    try {
      const response = await extensionFetch(CONFIG_URL, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      await readJson(response);
      setSaved(values);
      setStatus("Settings saved.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  const generate = async () => {
    if (dirty) await save();
    setRunning(true);
    setError(null);
    setStatus(null);
    try {
      const response = await extensionFetch(RUN_URL, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: "{}",
      });
      await readJson(response);
      setStatus("Cover generation started. Watch Cove’s task list for progress.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setRunning(false);
    }
  };

  if (loading) {
    return h("p", { className: "text-sm text-secondary" }, "Loading settings…");
  }

  return h("div", { className: "space-y-4" },
    h("p", { className: "text-sm text-secondary" },
      "Creates 16:9 movie-poster covers for audio and text items that have no image. Set the API key under Settings → AI Provider."),
    h(Toggle, {
      label: "Audio items",
      description: "Fill in missing covers using title, details, tags, and performers.",
      value: values.includeAudio,
      onChange: (value) => update("includeAudio", value),
    }),
    h(Toggle, {
      label: "Text items",
      description: "Fill in missing covers using the text file as image content.",
      value: values.includeText,
      onChange: (value) => update("includeText", value),
    }),
    h("div", { className: "grid gap-4 sm:grid-cols-2" },
      h(Field, { label: "Max items per run", description: "Empty or 0 means all missing covers." },
        h("input", {
          type: "text",
          inputMode: "numeric",
          value: values.maxItems,
          onChange: (event) => update("maxItems", event.target.value),
          placeholder: "all",
          className: inputClass(),
        })),
      h(Field, { label: "Max text characters", description: "How much of a text file to send as image content." },
        h("input", {
          type: "text",
          inputMode: "numeric",
          value: values.maxTextCharacters,
          onChange: (event) => update("maxTextCharacters", event.target.value),
          className: inputClass(),
        })),
    ),
    error ? h("p", { className: "text-sm text-red-400" }, error) : null,
    status ? h("p", { className: "text-sm text-secondary" }, status) : null,
    h("div", { className: "flex flex-wrap gap-2" },
      h("button", {
        type: "button",
        onClick: () => void save(),
        disabled: saving || !dirty,
        className: "px-3 py-1 text-xs bg-accent hover:bg-accent-hover rounded transition-colors disabled:opacity-50",
      }, saving ? "Saving…" : "Save settings"),
      h("button", {
        type: "button",
        onClick: () => { setValues(saved); setError(null); setStatus(null); },
        disabled: !dirty,
        className: "px-3 py-1 text-xs bg-card hover:bg-card-hover rounded transition-colors disabled:opacity-50",
      }, "Reset"),
      h("button", {
        type: "button",
        onClick: () => void generate(),
        disabled: running || saving,
        className: "px-3 py-1 text-xs bg-card hover:bg-card-hover rounded transition-colors disabled:opacity-50",
      }, running ? "Starting…" : "Generate missing covers"),
    ),
  );
}

export default {
  components: { SettingsPanel },
};
