using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TBM.Core.Interfaces.AI;
using TBM.Core.Models.AI;
using TBM.Infrastructure.AI.Models;
using TBM.Infrastructure.Configuration;

namespace TBM.Infrastructure.AI
{
    public class ReplicateAIProvider : IAIProvider
    {
        private readonly HttpClient _http;
        private readonly ReplicateSettings _settings;

        // Cached routing decision per model (resolved once per process lifetime).
        // UseVersionEndpoint = true  → POST /v1/predictions          with {"version":"…","input":{…}}
        // UseVersionEndpoint = false → POST /v1/models/{path}/predictions with {"input":{…}}
        private record ResolvedEndpoint(bool UseVersionEndpoint, string? Version);
        private static readonly ConcurrentDictionary<string, ResolvedEndpoint> _endpointCache = new();
        private static readonly SemaphoreSlim _resolveLock = new(1, 1);

        private const string DefaultNegativePrompt =
            "cartoonish, unrealistic, blurry, low quality, dark, cluttered, watermarks, "
          + "ugly, deformed, noisy, distorted, illustration, painting, sketch";

        public string ProviderName => "Replicate";

        public ReplicateAIProvider(
            HttpClient http,
            IOptions<ReplicateSettings> options)
        {
            _http = http;
            _settings = options.Value;

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
                throw new Exception("Replicate API key is NULL or EMPTY at runtime");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", _settings.ApiKey);
        }

        // ── IMAGE GENERATION ─────────────────────────────────────────────────────

        public async Task<AIProviderResult> GenerateImageAsync(AIImageRequest request)
        {
            var isImg2Img = !string.IsNullOrWhiteSpace(request.ImageUrl);
            Console.WriteLine($"[Replicate] Image mode: {(isImg2Img ? "IMG2IMG" : "TXT2IMG")}");

            // Model is config-driven (AI:Replicate:ImageModel). BuildImageInput picks
            // the correct Replicate input schema for whichever model is configured
            // (FLUX.1 Dev, adirik/interior-design, etc.) — switching models back and
            // forth is a config-only change, no code change required.
            var endpoint = await ResolveEndpointAsync(_settings.ImageModel);

            // Applied to every render regardless of room type (kitchen, living room,
            // bathroom, bedroom, exterior, etc.) — the caller's own request.Prompt
            // carries the room-specific ask (e.g. "make my kitchen fine"); this
            // wraps it in TBM's standard Nigerian-market luxury photography style
            // so every generation reads as a consistent, on-brand magazine render.
            var designPrompt =
                $"A completely redesigned and redecorated interior. {request.Prompt}. " +
                "Nigerian luxury interior design, contemporary African aesthetic, Lagos Abuja upscale home, " +
                "warm ambient lighting, premium finishes, natural light. " +
                "modern finishes, clean contemporary design, comfortable upscale living. " +
                "photorealistic architectural visualization, professional interior photography, " +
                "ultra detailed, sharp focus, magazine quality, 8K resolution.";

            var input = BuildImageInput(request, designPrompt);

            return await SubmitAndPollAsync(endpoint, input);
        }

        /// <summary>
        /// Builds the Replicate "input" payload for the configured image model.
        /// Each model has its own input schema, so this switches on
        /// <c>_settings.ImageModel</c> rather than hardcoding one schema —
        /// changing the config value is enough to switch models.
        /// </summary>
        private object BuildImageInput(AIImageRequest request, string enhancedPrompt)
        {
            var model = _settings.ImageModel;

            if (model.Contains("flux"))
            {
                // FLUX.1 Dev input schema
                var input = new Dictionary<string, object>
                {
                    ["prompt"] = enhancedPrompt,
                    ["num_inference_steps"] = 28,
                    ["guidance"] = 3.5,
                    ["output_format"] = "webp",
                    ["output_quality"] = 90,
                    ["go_fast"] = false,
                    ["megapixels"] = "1",
                    ["num_outputs"] = 1,
                    ["aspect_ratio"] = string.IsNullOrWhiteSpace(request.ImageUrl)
                        ? "16:9"
                        : "1:1"
                };

                // img2img mode - only if source image provided
                if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                {
                    input["image"] = request.ImageUrl;
                    input["strength"] = 0.85;
                    input["prompt_strength"] = 0.8;
                }

                if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
                {
                    // FLUX doesn't use negative_prompt natively
                    // append as "avoid:" to the main prompt instead
                    input["prompt"] = enhancedPrompt +
                        $" Avoid: {request.NegativePrompt}";
                }

                return input;
            }

            // Fallback: adirik/interior-design (existing schema)
            var adrikInput = new Dictionary<string, object>
            {
                ["prompt"] = enhancedPrompt,
                ["negative_prompt"] = request.NegativePrompt ?? DefaultNegativePrompt,
                ["guidance_scale"] = 15,
                ["num_inference_steps"] = 50
            };

            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                adrikInput["image"] = request.ImageUrl;
                adrikInput["strength"] = 0.99;
            }

            return adrikInput;
        }

        // ── VIDEO GENERATION ─────────────────────────────────────────────────────

        public async Task<AIProviderResult> GenerateVideoAsync(AIVideoRequest request)
        {
            Console.WriteLine($"[Replicate] Video generation started");
            Console.WriteLine($"[Replicate] Prompt  : {request.Prompt}");
            Console.WriteLine($"[Replicate] Image   : {request.ImageUrl ?? "None (text-to-video)"}");

            var hasImage = !string.IsNullOrWhiteSpace(request.ImageUrl);

            // Model is config-driven (AI:Replicate:VideoModel). BuildVideoInput picks
            // the correct Replicate input schema for whichever model is configured
            // (Luma Dream Machine, kwaivgi/kling-v1.6-standard, etc.).
            var endpoint = await ResolveEndpointAsync(_settings.VideoModel);

            // User's prompt drives the content. We wrap it with a narrative arc:
            // construction/renovation work IN PROGRESS → ending with the beautiful finished result revealed.
            var enrichedPrompt =
                $"{request.Prompt}. " +
                "Workers actively doing the renovation work. " +
                "The video ends with the completed transformation — a beautifully finished interior revealed. " +
                "Cinematic camera angles, smooth motion, photorealistic, high quality, 4K.";

            var input = BuildVideoInput(request, enrichedPrompt);

            // Kling v1.6 typically takes 3–4 minutes. 300 × 2s = 10 min max.
            return await SubmitAndPollAsync(endpoint, input, maxAttempts: 300);
        }

        /// <summary>
        /// Builds the Replicate "input" payload for the configured video model.
        /// Each model has its own input schema, so this switches on
        /// <c>_settings.VideoModel</c> rather than hardcoding one schema —
        /// changing the config value is enough to switch models.
        /// </summary>
        private object BuildVideoInput(AIVideoRequest request, string enhancedPrompt)
        {
            var model = _settings.VideoModel;
            var duration = request.DurationSeconds > 0
                ? request.DurationSeconds
                : 5; // fix the bug where duration was hardcoded 10

            if (model.Contains("luma") || model.Contains("dream-machine"))
            {
                // Luma Dream Machine input schema
                var input = new Dictionary<string, object>
                {
                    ["prompt"] = enhancedPrompt,
                    ["loop"] = false,
                    ["aspect_ratio"] = "16:9"
                };

                // Luma uses keyframes for image-to-video
                if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                {
                    input["keyframes"] = new
                    {
                        frame0 = new
                        {
                            type = "image",
                            url = request.ImageUrl
                        }
                    };
                }

                return input;
            }

            // Fallback: Kling (existing schema)
            var klingInput = new Dictionary<string, object>
            {
                ["prompt"] = enhancedPrompt,
                ["duration"] = duration,
                ["cfg_scale"] = 0.7,
                ["negative_prompt"] = "low quality, blurry, distorted"
            };

            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                klingInput["start_image"] = request.ImageUrl;
            }
            else
            {
                klingInput["aspect_ratio"] = "16:9";
            }

            return klingInput;
        }

        // ── ENDPOINT RESOLUTION ───────────────────────────────────────────────────

        /// <summary>
        /// Determines the correct Replicate API endpoint for <paramref name="modelPath"/>:
        /// <list type="bullet">
        ///   <item>Community models (e.g. adirik/interior-design) expose a versions list →
        ///         use the version-hash endpoint so they never return 404.</item>
        ///   <item>Officially deployed models (e.g. minimax/video-01) return 404 on the
        ///         versions endpoint → use the model-path endpoint instead.</item>
        /// </list>
        /// The result is cached permanently per model so this lookup runs only once.
        /// </summary>
        private async Task<ResolvedEndpoint> ResolveEndpointAsync(string modelPath)
        {
            if (_endpointCache.TryGetValue(modelPath, out var cached))
                return cached;

            await _resolveLock.WaitAsync();
            try
            {
                if (_endpointCache.TryGetValue(modelPath, out cached))
                    return cached;

                Console.WriteLine($"[Replicate] Resolving endpoint for: {modelPath}");

                var versionsResponse = await _http.GetAsync(
                    $"https://api.replicate.com/v1/models/{modelPath}/versions");

                ResolvedEndpoint resolved;

                if (versionsResponse.IsSuccessStatusCode)
                {
                    // Community model — parse the latest version hash
                    var rawJson = await versionsResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(rawJson);
                    var first = doc.RootElement
                        .GetProperty("results")
                        .EnumerateArray()
                        .FirstOrDefault();

                    if (first.ValueKind == JsonValueKind.Undefined)
                        throw new InvalidOperationException(
                            $"No versions found for model '{modelPath}'.");

                    var versionHash = first.GetProperty("id").GetString()
                        ?? throw new InvalidOperationException(
                            $"Version ID was null for model '{modelPath}'.");

                    resolved = new ResolvedEndpoint(UseVersionEndpoint: true, Version: versionHash);
                    Console.WriteLine($"[Replicate] {modelPath} → version-hash endpoint ({versionHash[..12]}…)");
                }
                else
                {
                    // Officially deployed model — use the model-path endpoint
                    resolved = new ResolvedEndpoint(UseVersionEndpoint: false, Version: null);
                    Console.WriteLine($"[Replicate] {modelPath} → model-path endpoint");
                }

                _endpointCache[modelPath] = resolved;
                return resolved;
            }
            finally
            {
                _resolveLock.Release();
            }
        }

        // ── PREDICTION SUBMIT + POLL ──────────────────────────────────────────────

        private async Task<AIProviderResult> SubmitAndPollAsync(
            ResolvedEndpoint endpoint,
            object input,
            int maxAttempts = 60)
        {
            string apiUrl;
            object payload;

            if (endpoint.UseVersionEndpoint)
            {
                // Version-hash endpoint: POST /v1/predictions
                // Works for all models (community + official).
                apiUrl = "https://api.replicate.com/v1/predictions";
                payload = new { version = endpoint.Version, input };
            }
            else
            {
                // Model-path endpoint: POST /v1/models/{owner}/{model}/predictions
                // Used for officially deployed models that don't expose versions.
                // Determine the model path from the cached entry's absence of a version.
                // We need the model path — extract it by reverse-looking in the cache.
                var modelPath = _endpointCache
                    .FirstOrDefault(kv => kv.Value == endpoint)
                    .Key;
                apiUrl = $"https://api.replicate.com/v1/models/{modelPath}/predictions";
                payload = new { input };
            }

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            Console.WriteLine($"[Replicate] POST {apiUrl}");

            var response = await _http.PostAsync(
                apiUrl,
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

            var rawResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Replicate] Submit: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Replicate API returned {response.StatusCode}: {rawResponse}");

            var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;
            return await PollPredictionAsync(prediction.Id, maxAttempts);
        }

        private async Task<AIProviderResult> PollPredictionAsync(
            string predictionId,
            int maxAttempts)
        {
            Console.WriteLine($"[Replicate] Polling: {predictionId} (max {maxAttempts * 2}s)");

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await Task.Delay(2000);

                var response = await _http.GetAsync(
                    $"https://api.replicate.com/v1/predictions/{predictionId}");

                response.EnsureSuccessStatusCode();

                var rawResponse = await response.Content.ReadAsStringAsync();
                var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;

                Console.WriteLine($"[Replicate] Poll {attempt}/{maxAttempts}: {prediction.Status}");

                if (prediction.Status == "succeeded")
                {
                    var outputUrl = ExtractOutputUrl(prediction.Output);
                    Console.WriteLine($"[Replicate] Success. Output: {outputUrl}");

                    return new AIProviderResult
                    {
                        Success = true,
                        OutputUrl = outputUrl,
                        Cost = 0.05m,
                        RawResponse = rawResponse,
                        ProviderJobId = predictionId
                    };
                }

                if (prediction.Status is "failed" or "canceled")
                {
                    var error = prediction.Error ?? "Unknown error";
                    Console.WriteLine($"[Replicate] Failed: {error}");

                    return new AIProviderResult
                    {
                        Success = false,
                        OutputUrl = string.Empty,
                        Cost = 0m,
                        RawResponse = rawResponse,
                        ProviderJobId = predictionId,
                        ErrorMessage = error
                    };
                }
            }

            throw new TimeoutException(
                $"Prediction {predictionId} timed out after {maxAttempts * 2} seconds.");
        }

        private static string ExtractOutputUrl(object? output)
        {
            if (output == null) return string.Empty;

            var element = (JsonElement)output;

            return element.ValueKind switch
            {
                // Video models (minimax) and adirik/interior-design return a single string URL
                JsonValueKind.String => element.GetString() ?? string.Empty,
                // Some models return an array of URLs — take the first
                JsonValueKind.Array => element.EnumerateArray()
                    .FirstOrDefault()
                    .GetString() ?? string.Empty,
                _ => string.Empty
            };
        }
    }
}
