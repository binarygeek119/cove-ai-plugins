import { createElement as h, useEffect, useState } from "@cove/runtime/react";
import { extensionFetch } from "@cove/runtime/api";

const EXTENSION_ID = "com.binarygeek119.ai-provider";
const CONFIG_URL = `/api/plugins/${encodeURIComponent(EXTENSION_ID)}/config`;

const defaults = {
  apiKey: "",
  baseUrl: "https://api.venice.ai/api/v1",
  chatModel: "",
  speechModel: "tts-kokoro",
  speechVoice: "af_sky",
  speechFormat: "mp3",
};

const formats = ["mp3", "opus", "aac", "flac", "wav"];

function asString(value, fallback = "") {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return fallback;
}

function fromConfig(raw) {
  return {
    apiKey: asString(raw?.apiKey),
    baseUrl: asString(raw?.baseUrl, defaults.baseUrl) || defaults.baseUrl,
    chatModel: asString(raw?.chatModel),
    speechModel: asString(raw?.speechModel, defaults.speechModel) || defaults.speechModel,
    speechVoice: asString(raw?.speechVoice, defaults.speechVoice) || defaults.speechVoice,
    speechFormat: asString(raw?.speechFormat, defaults.speechFormat) || defaults.speechFormat,
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

function SettingsPanel() {
  const [values, setValues] = useState(defaults);
  const [saved, setSaved] = useState(defaults);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
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
      setStatus("Settings saved. Other AI plugins will use this provider.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return h("p", { className: "text-sm text-secondary" }, "Loading settings…");
  }

  return h("div", { className: "space-y-4" },
    h("p", { className: "text-sm text-secondary" },
      "This plugin is the shared AI backend. Text to Audio and other plugins call it for chat and speech instead of storing their own API key."),
    h(Field, { label: "API key", description: "Falls back to VENICE_API_KEY, then OPENAI_API_KEY, when empty." },
      h("input", {
        type: "password",
        autoComplete: "off",
        value: values.apiKey,
        onChange: (event) => update("apiKey", event.target.value),
        className: inputClass(),
      })),
    h(Field, { label: "API URL", description: "OpenAI-compatible root. Default is https://api.venice.ai/api/v1." },
      h("input", {
        type: "url",
        value: values.baseUrl,
        onChange: (event) => update("baseUrl", event.target.value),
        placeholder: "https://api.venice.ai/api/v1",
        className: inputClass(),
      })),
    h(Field, { label: "Chat model", description: "Used by plugins that call chat. Leave empty until you need chat." },
      h("input", {
        type: "text",
        value: values.chatModel,
        onChange: (event) => update("chatModel", event.target.value),
        placeholder: "optional",
        className: inputClass(),
      })),
    h("div", { className: "grid gap-4 sm:grid-cols-3" },
      h(Field, { label: "Speech model" },
        h("input", {
          type: "text",
          value: values.speechModel,
          onChange: (event) => update("speechModel", event.target.value),
          className: inputClass(),
        })),
      h(Field, { label: "Speech voice" },
        h("input", {
          type: "text",
          value: values.speechVoice,
          onChange: (event) => update("speechVoice", event.target.value),
          className: inputClass(),
        })),
      h(Field, { label: "Speech format" },
        h("select", {
          value: values.speechFormat,
          onChange: (event) => update("speechFormat", event.target.value),
          className: inputClass(),
        }, formats.map((format) => h("option", { key: format, value: format }, format)))),
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
    ),
  );
}

export default {
  components: { SettingsPanel },
};
