import { createElement as h, useEffect, useState } from "@cove/runtime/react";
import { extensionFetch } from "@cove/runtime/api";

const EXTENSION_ID = "com.binarygeek119.cove-lounge";
const CONFIG_URL = `/api/plugins/${encodeURIComponent(EXTENSION_ID)}/config`;
const APPS_URL = `/api/plugins/${encodeURIComponent(EXTENSION_ID)}/apps`;
const CONNECT_URL = `/api/plugins/${encodeURIComponent(EXTENSION_ID)}/connect`;

const defaults = {
  relayUrl: "https://covelounge.duckdns.org",
  covePublicUrl: "",
  coveInternalUrl: "",
  apiKey: "",
};

function asString(value, fallback = "") {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return fallback;
}

function fromConfig(raw) {
  return {
    relayUrl: asString(raw?.relayUrl, defaults.relayUrl) || defaults.relayUrl,
    covePublicUrl: asString(raw?.covePublicUrl),
    coveInternalUrl: asString(raw?.coveInternalUrl),
    apiKey: asString(raw?.apiKey),
  };
}

async function readJson(response) {
  const body = await response.text().catch(() => "");
  if (!response.ok) throw new Error(body || `Request failed (${response.status})`);
  if (!body.trim()) return undefined;
  return JSON.parse(body);
}

function inputClass() {
  return "w-full bg-card border border-border rounded px-2 py-1 text-sm focus:border-accent outline-none";
}

function Field({ label, description, children }) {
  return h("label", { className: "block space-y-1" },
    h("span", { className: "text-sm font-medium" }, label),
    description ? h("span", { className: "block text-xs text-secondary" }, description) : null,
    children,
  );
}

function SettingsPanel() {
  const [values, setValues] = useState(defaults);
  const [saved, setSaved] = useState(defaults);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [status, setStatus] = useState(null);

  const [pin, setPin] = useState("");
  const [label, setLabel] = useState("");
  const [connecting, setConnecting] = useState(false);
  const [connectStatus, setConnectStatus] = useState(null);
  const [apps, setApps] = useState([]);
  const [appsLoading, setAppsLoading] = useState(true);

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
    refreshApps();
    return () => { cancelled = true; };
  }, []);

  async function refreshApps() {
    setAppsLoading(true);
    try {
      const response = await extensionFetch(APPS_URL);
      const data = await readJson(response);
      setApps(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setAppsLoading(false);
    }
  }

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

  const connect = async () => {
    setConnecting(true);
    setConnectStatus(null);
    setError(null);
    try {
      const response = await extensionFetch(CONNECT_URL, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ pin, label }),
      });
      await readJson(response);
      setConnectStatus("Connected.");
      setPin("");
      setLabel("");
      await refreshApps();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setConnecting(false);
    }
  };

  const removeApp = async (appId) => {
    try {
      await extensionFetch(`${APPS_URL}/${encodeURIComponent(appId)}`, { method: "DELETE" });
      await refreshApps();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  if (loading) {
    return h("p", { className: "text-sm text-secondary" }, "Loading settings…");
  }

  return h("div", { className: "space-y-4" },
    h("p", { className: "text-sm text-secondary" },
      "Connect a CoveLounge app by entering the PIN it displays. This server sends its Cove URLs and API key through the quick PIN relay."),
    h(Field, { label: "Relay URL", description: "Quick PIN relay origin." },
      h("input", {
        type: "url",
        value: values.relayUrl,
        onChange: (event) => update("relayUrl", event.target.value),
        className: inputClass(),
      })),
    h(Field, { label: "Cove public URL", description: "Internet-reachable Cove URL." },
      h("input", {
        type: "url",
        value: values.covePublicUrl,
        onChange: (event) => update("covePublicUrl", event.target.value),
        className: inputClass(),
      })),
    h(Field, { label: "Cove internal URL", description: "LAN/internal Cove URL." },
      h("input", {
        type: "url",
        value: values.coveInternalUrl,
        onChange: (event) => update("coveInternalUrl", event.target.value),
        className: inputClass(),
      })),
    h(Field, { label: "Cove API key", description: "API key the CoveLounge app will use." },
      h("input", {
        type: "password",
        autoComplete: "off",
        value: values.apiKey,
        onChange: (event) => update("apiKey", event.target.value),
        className: inputClass(),
      })),
    error ? h("p", { className: "text-sm text-red-400" }, error) : null,
    status ? h("p", { className: "text-sm text-secondary" }, status) : null,
    h("div", { className: "flex flex-wrap gap-2" },
      h("button", {
        type: "button",
        onClick: () => void save(),
        disabled: saving || !dirty,
        className: "px-3 py-1 text-xs bg-accent hover:bg-accent-hover rounded transition-colors disabled:opacity-50",
      }, saving ? "Saving…" : "Save settings"),
    ),
    h("hr", { className: "border-border" }),
    h("h3", { className: "text-sm font-medium" }, "Connect app"),
    h(Field, { label: "PIN", description: "PIN displayed by the CoveLounge app." },
      h("input", {
        type: "text",
        value: pin,
        onChange: (event) => setPin(event.target.value),
        className: inputClass(),
      })),
    h(Field, { label: "Label (optional)" },
      h("input", {
        type: "text",
        value: label,
        onChange: (event) => setLabel(event.target.value),
        className: inputClass(),
      })),
    connectStatus ? h("p", { className: "text-sm text-secondary" }, connectStatus) : null,
    h("button", {
      type: "button",
      onClick: () => void connect(),
      disabled: connecting || !pin.trim(),
      className: "px-3 py-1 text-xs bg-accent hover:bg-accent-hover rounded transition-colors disabled:opacity-50",
    }, connecting ? "Connecting…" : "Connect"),
    h("hr", { className: "border-border" }),
    h("h3", { className: "text-sm font-medium" }, "Connected apps"),
    appsLoading ? h("p", { className: "text-sm text-secondary" }, "Loading…") : null,
    h("ul", { className: "space-y-2" },
      apps.map((app) => h("li", { key: app.id, className: "flex items-center justify-between gap-2 text-sm" },
        h("span", null, `${app.label} — ${app.serverUrl}`),
        h("button", {
          type: "button",
          onClick: () => void removeApp(app.id),
          className: "px-2 py-1 text-xs bg-card hover:bg-card-hover rounded",
        }, "Remove"),
      )),
    ),
  );
}

export default {
  components: { SettingsPanel },
};
