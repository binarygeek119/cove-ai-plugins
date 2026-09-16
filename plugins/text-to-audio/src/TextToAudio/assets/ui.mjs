import { createElement as h, useEffect, useState } from "@cove/runtime/react";
import { extensionFetch } from "@cove/runtime/api";

const EXTENSION_ID = "com.binarygeek119.text-to-audio";
const CONFIG_URL = `/api/plugins/${encodeURIComponent(EXTENSION_ID)}/config`;
const RUN_URL = `/api/extensions/${encodeURIComponent(EXTENSION_ID)}/jobs/convert-text-files/run`;

const defaults = {
  inputFolder: "",
  outputFolder: "",
  voice: "af_sky",
  model: "tts-kokoro",
  format: "mp3",
  importIntoLibrary: true,
  skipExisting: true,
};

const modelCatalog = [
  { id: "tts-kokoro", label: "Kokoro", voices: ["af_alloy", "af_aoede", "af_bella", "af_heart", "af_jadzia", "af_jessica", "af_kore", "af_nicole", "af_nova", "af_river", "af_sarah", "af_sky", "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx", "am_puck", "am_santa", "bf_alice", "bf_emma", "bf_lily", "bm_daniel", "bm_fable", "bm_george", "bm_lewis"] },
  { id: "tts-xai-v1", label: "xAI TTS v1", voices: ["altair", "ara", "atlas", "carina", "castor", "celeste", "cosmo", "eve", "helios", "helix", "iris", "kepler", "leo", "lumen", "luna", "lux", "naksh", "orion", "perseus", "rex", "rigel", "sal", "sirius", "ursa", "zagan", "zenith"] },
  { id: "tts-orpheus", label: "Orpheus", voices: ["dan", "jess", "leah", "leo", "mia", "tara", "zac", "zoe"] },
  { id: "tts-qwen3-0-6b", label: "Qwen 3 TTS 0.6B", voices: ["Aiden", "Dylan", "Eric", "Ono_Anna", "Ryan", "Serena", "Sohee", "Uncle_Fu", "Vivian"] },
  { id: "tts-qwen3-1-7b", label: "Qwen 3 TTS 1.7B", voices: ["Aiden", "Dylan", "Eric", "Ono_Anna", "Ryan", "Serena", "Sohee", "Uncle_Fu", "Vivian"] },
  { id: "tts-inworld-1-5-max", label: "Inworld TTS-1.5 Max", voices: ["Alex", "Ashley", "Craig", "Edward", "Elizabeth", "Hades", "Luna", "Mark", "Olivia", "Pixie", "Priya", "Ronald", "Sarah", "Theodore"] },
  { id: "tts-chatterbox-hd", label: "Chatterbox HD", voices: ["Aurora", "Blade", "Britney", "Carl", "Cliff", "Richard", "Rico", "Siobhan", "Vicky"] },
  { id: "tts-elevenlabs-turbo-v2-5", label: "ElevenLabs Turbo v2.5", voices: ["Alice", "Aria", "Bill", "Brian", "Callum", "Charlie", "Charlotte", "Chris", "Daniel", "Eric", "George", "Jessica", "Laura", "Liam", "Lily", "Matilda", "Rachel", "River", "Roger", "Sarah", "Will"] },
  { id: "tts-minimax-speech-02-hd", label: "MiniMax Speech-02 HD", voices: ["CalmWoman", "CasualGuy", "DeepVoiceMan", "DeterminedMan", "ElegantMan", "ExuberantGirl", "FriendlyPerson", "ImposingManner", "InspirationalGirl", "LivelyGirl", "LovelyGirl", "PatientMan", "SweetGirl", "WiseWoman", "YoungKnight"] },
  { id: "tts-gradium-v1", label: "Gradium TTS", voices: ["Alice", "Davi", "Elise", "Emma", "Eva", "Jack", "Kent", "Leo", "Maximilian", "Mia", "Sergio", "Valentina"] },
  { id: "tts-gemini-3-1-flash", label: "Gemini 3.1 Flash TTS", voices: ["Achernar", "Achird", "Algenib", "Algieba", "Alnilam", "Aoede", "Autonoe", "Callirrhoe", "Charon", "Despina", "Enceladus", "Erinome", "Fenrir", "Gacrux", "Iapetus", "Kore", "Laomedeia", "Leda", "Orus", "Puck", "Pulcherrima", "Rasalgethi", "Sadachbia", "Sadaltager", "Schedar", "Sulafat", "Umbriel", "Vindemiatrix", "Zephyr", "Zubenelgenubi"] },
];
const formats = ["mp3", "opus", "aac", "flac", "wav"];

function voicesFor(model) {
  return modelCatalog.find((item) => item.id === model)?.voices ?? [];
}

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
    inputFolder: asString(raw?.inputFolder),
    outputFolder: asString(raw?.outputFolder),
    voice: asString(raw?.voice, defaults.voice) || defaults.voice,
    model: asString(raw?.model, defaults.model) || defaults.model,
    format: asString(raw?.format, defaults.format) || defaults.format,
    importIntoLibrary: asBool(raw?.importIntoLibrary, defaults.importIntoLibrary),
    skipExisting: asBool(raw?.skipExisting, defaults.skipExisting),
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

  const setModel = (model) => {
    const nextVoices = voicesFor(model);
    setValues((current) => ({
      ...current,
      model,
      voice: nextVoices.includes(current.voice) ? current.voice : (nextVoices[0] ?? current.voice),
    }));
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

  const convert = async () => {
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
      setStatus("Conversion started. Watch Cove’s task list for progress.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setRunning(false);
    }
  };

  const modelVoices = voicesFor(values.model);
  const voiceOptions = modelVoices.includes(values.voice) || !values.voice
    ? modelVoices
    : [values.voice, ...modelVoices];
  const modelOptions = modelCatalog.some((item) => item.id === values.model)
    ? modelCatalog
    : [{ id: values.model, label: values.model, voices: voiceOptions }, ...modelCatalog];

  if (loading) {
    return h("p", { className: "text-sm text-secondary" }, "Loading settings…");
  }

  return h("div", { className: "space-y-4" },
    h("p", { className: "text-sm text-secondary" },
      "Text files from the input folder are sent through the shared AI Provider plugin and written into the output folder. Set the API key under Settings → AI Provider."),
    h(Field, { label: "Input folder", description: "Searched recursively for .txt and .md files." },
      h("input", {
        type: "text",
        value: values.inputFolder,
        onChange: (event) => update("inputFolder", event.target.value),
        placeholder: "/path/to/text",
        className: inputClass(),
      })),
    h(Field, { label: "Output folder", description: "Generated audio is written here." },
      h("input", {
        type: "text",
        value: values.outputFolder,
        onChange: (event) => update("outputFolder", event.target.value),
        placeholder: "/path/to/audio",
        className: inputClass(),
      })),
    h("div", { className: "grid gap-4 sm:grid-cols-3" },
      h(Field, { label: "Model" },
        h("select", {
          value: values.model,
          onChange: (event) => setModel(event.target.value),
          className: inputClass(),
        }, modelOptions.map((item) => h("option", { key: item.id, value: item.id }, item.label)))),
      h(Field, { label: "Voice" },
        h("select", {
          value: values.voice,
          onChange: (event) => update("voice", event.target.value),
          className: inputClass(),
        }, voiceOptions.map((voice) => h("option", { key: voice, value: voice }, voice)))),
      h(Field, { label: "Format" },
        h("select", {
          value: values.format,
          onChange: (event) => update("format", event.target.value),
          className: inputClass(),
        }, formats.map((format) => h("option", { key: format, value: format }, format)))),
    ),
    h(Toggle, {
      label: "Import into Cove library",
      description: "Create or update a Cove audio item after each file is written.",
      value: values.importIntoLibrary,
      onChange: (value) => update("importIntoLibrary", value),
    }),
    h(Toggle, {
      label: "Skip existing audio",
      description: "Leave files that already exist in the output folder alone.",
      value: values.skipExisting,
      onChange: (value) => update("skipExisting", value),
    }),
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
        onClick: () => void convert(),
        disabled: running || saving,
        className: "px-3 py-1 text-xs bg-card hover:bg-card-hover rounded transition-colors disabled:opacity-50",
      }, running ? "Starting…" : "Convert text files"),
    ),
  );
}

export default {
  components: { SettingsPanel },
};
