<h1>Phi-3 Mini Instruct ONNX Chatbot</h1>

<p>
  Console chatbot using <strong>Microsoft.ML.OnnxRuntimeGenAI</strong> with Phi-3 Mini Instruct ONNX weights
  downloaded from Hugging Face at runtime.
</p>

<h2>Dependencies</h2>

<h3>Required</h3>
<ul>
  <li><strong>.NET SDK</strong>: <code>8.0</code> (targets <code>net8.0</code>)</li>
  <li><strong>NuGet</strong>: <code>Microsoft.ML.OnnxRuntimeGenAI</code> (CPU) <code>0.6.0</code></li>
</ul>

<h3>Built-in libraries (BCL)</h3>
<ul>
  <li><code>System</code></li>
  <li><code>System.IO</code></li>
  <li><code>System.Net</code></li>
  <li><code>System.Net.Http</code></li>
  <li><code>System.Text</code></li>
  <li><code>System.Text.Json</code></li>
  <li><code>System.Threading</code></li>
</ul>

<h2>Models (downloaded at runtime)</h2>

<h3>Hugging Face repo</h3>
<ul>
  <li><strong>Repo</strong>: <code>microsoft/Phi-3-mini-4k-instruct-onnx</code></li>
  <li><strong>Base URL</strong>:
    <code>https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/resolve/main/</code>
  </li>
</ul>

<h3>Variant selection</h3>
<p>
  The app probes for <code>genai_config.json</code> in these subfolders (in order) and uses the first one found:
</p>
<ul>
  <li><code>cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/</code></li>
  <li><code>cpu_and_mobile/cpu-int4-rtn-block-32/</code></li>
  <li><code>directml/directml-int4-awq-block-128/</code></li>
  <li><code>cuda/cuda-fp16/</code></li>
  <li><code>cuda/cuda-int4-rtn-block-32/</code></li>
</ul>

<h3>Files downloaded</h3>

<h4>Required</h4>
<ul>
  <li><code>genai_config.json</code></li>
  <li><code>config.json</code></li>
  <li><code>tokenizer.json</code></li>
  <li><code>tokenizer_config.json</code></li>
  <li><code>special_tokens_map.json</code></li>
  <li>
    <strong>Model ONNX</strong> (filename is read from <code>genai_config.json</code> at
    <code>model.decoder.filename</code>)
  </li>
</ul>

<h4>Optional (downloaded if present)</h4>
<ul>
  <li><code>&lt;model&gt;.onnx.data</code> (external weights, when used)</li>
  <li><code>added_tokens.json</code></li>
  <li><code>tokenizer.model</code></li>
  <li><code>vocab.json</code></li>
  <li><code>merges.txt</code></li>
  <li><code>configuration_phi3.py</code></li>
</ul>

<h3>Local cache</h3>
<ul>
  <li><strong>Windows</strong>: <code>%LOCALAPPDATA%\Phi3Chatbot\model\&lt;variant-folder&gt;</code></li>
</ul>