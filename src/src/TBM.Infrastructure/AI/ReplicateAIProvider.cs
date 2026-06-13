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

        public string ProviderName => "Replicate";

        public ReplicateAIProvider(
            HttpClient http,
            IOptions<ReplicateSettings> options)
        {
            _http = http;
            _settings = options.Value;
            
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new Exception("❌ Replicate API key is NULL or EMPTY at runtime");
            }
            
            // ✅ CRITICAL: Replicate API uses "Token" not "Bearer"
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", _settings.ApiKey);
        }

        public async Task<AIProviderResult> GenerateImageAsync(AIImageRequest request)
{
    // ✅ DETECT: img2img vs txt2img based on ImageUrl presence
    var isImg2Img = !string.IsNullOrWhiteSpace(request.ImageUrl);
    
    Console.WriteLine($"[Replicate] Mode: {(isImg2Img ? "IMG2IMG" : "TXT2IMG")}");
    
    object payload;
    
    if (isImg2Img)
    {
        // ✅ IMG2IMG: Transform existing image
        payload = new
        {
            version = "39ed52f2a78e934b3ba6e2a89f5b1c712de7dfea535525255b1aa35c5565e08b",
            input = new
            {
                image = request.ImageUrl, // ✅ Source image
                prompt = request.Prompt,
                negative_prompt = request.NegativePrompt ?? "blurry, low quality, distorted, deformed",
                num_outputs = 1,
                num_inference_steps = 30,
                guidance_scale = 7.5,
                prompt_strength = 0.8, // ✅ How much to transform (0.0-1.0)
                scheduler = "K_EULER"
            }
        };
    }
    else
    {
        // ✅ TXT2IMG: Generate from scratch
        payload = new
        {
            version = "39ed52f2a78e934b3ba6e2a89f5b1c712de7dfea535525255b1aa35c5565e08b",
            input = new
            {
                prompt = request.Prompt,
                negative_prompt = request.NegativePrompt ?? "blurry, low quality, distorted, deformed",
                num_outputs = 1,
                width = request.Width,
                height = request.Height,
                num_inference_steps = 30,
                guidance_scale = 7.5
            }
        };
    }

    var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });

    Console.WriteLine($"[Replicate] Sending: {jsonPayload}");

    var response = await _http.PostAsync(
        "https://api.replicate.com/v1/predictions",
        new StringContent(jsonPayload, Encoding.UTF8, "application/json")
    );

    var rawResponse = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"[Replicate] Status: {response.StatusCode}");
    Console.WriteLine($"[Replicate] Response: {rawResponse}");

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"Replicate API returned {response.StatusCode}: {rawResponse}");
    }

    var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;
    
    return await PollPredictionAsync(prediction.Id);
}



     private async Task<AIProviderResult> PollPredictionAsync(string predictionId, int maxAttempts = 60)
{
    var attempts = 0;

    Console.WriteLine($"[Replicate] Polling prediction: {predictionId}");
    Console.WriteLine($"[Replicate] Max wait time: {maxAttempts * 2} seconds");

    while (attempts < maxAttempts)
    {
        await Task.Delay(2000);
        attempts++;

        var response = await _http.GetAsync(
            $"https://api.replicate.com/v1/predictions/{predictionId}");
        
        response.EnsureSuccessStatusCode();
        
        var rawResponse = await response.Content.ReadAsStringAsync();
        var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;

        Console.WriteLine($"[Replicate] Poll #{attempts}/{maxAttempts}: Status={prediction.Status}");

        if (prediction.Status == "succeeded")
        {
            // ✅ Handle both string (video) and array (image) outputs
            string outputUrl = string.Empty;
            
            if (prediction.Output != null)
            {
                var outputElement = (JsonElement)prediction.Output;
                
                if (outputElement.ValueKind == JsonValueKind.String)
                {
                    // Video output - single string URL
                    outputUrl = outputElement.GetString() ?? string.Empty;
                }
                else if (outputElement.ValueKind == JsonValueKind.Array)
                {
                    // Image output - array of URLs
                    outputUrl = outputElement.EnumerateArray().FirstOrDefault().GetString() ?? string.Empty;
                }
            }
            
            Console.WriteLine($"[Replicate] ✅ Success! Output: {outputUrl}");
            
            return new AIProviderResult
            {
                Success = true,
                OutputUrl = outputUrl,
                Cost = 0.05m,
                RawResponse = rawResponse,
                ProviderJobId = predictionId
            };
        }

        if (prediction.Status == "failed" || prediction.Status == "canceled")
        {
            var error = prediction.Error ?? "Unknown error";
            Console.WriteLine($"[Replicate] ❌ Failed: {error}");
            
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

    Console.WriteLine($"[Replicate] ⏱️ Timeout after {maxAttempts * 2} seconds");
    throw new TimeoutException(
        $"Prediction {predictionId} timed out after {maxAttempts * 2} seconds");
}

public async Task<AIProviderResult> GenerateVideoAsync(AIVideoRequest request)
{
    Console.WriteLine($"[Replicate] Video Generation Started");
    Console.WriteLine($"[Replicate] Prompt: {request.Prompt}");
    Console.WriteLine($"[Replicate] Image: {request.ImageUrl ?? "None (text-to-video)"}");
    
    var hasImage = !string.IsNullOrWhiteSpace(request.ImageUrl);
    var duration = request.DurationSeconds > 0 ? request.DurationSeconds : 9;
    
    object payload;
    
    if (hasImage)
    {
        payload = new
        {
            version = "3ca2bc3597e124149bcae1f9c239790a58ba0f1aa72e1c8747192d2b44284dc4",
            input = new
            {
                prompt = request.Prompt,
                image = request.ImageUrl,
                duration = duration,
                aspect_ratio = "16:9",
                loop = false
            }
        };
    }
    else
    {
        payload = new
        {
            version = "3ca2bc3597e124149bcae1f9c239790a58ba0f1aa72e1c8747192d2b44284dc4",
            input = new
            {
                prompt = request.Prompt,
                duration = duration,
                aspect_ratio = "16:9",
                loop = false
            }
        };
    }

    var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });

    Console.WriteLine($"[Replicate] Sending: {jsonPayload}");

    var response = await _http.PostAsync(
        "https://api.replicate.com/v1/predictions",
        new StringContent(jsonPayload, Encoding.UTF8, "application/json")
    );

    var rawResponse = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"[Replicate] Status: {response.StatusCode}");

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"[Replicate] ❌ Error: {rawResponse}");
        throw new HttpRequestException(
            $"Replicate API returned {response.StatusCode}: {rawResponse}");
    }

    var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;
    Console.WriteLine($"[Replicate] ✅ Video prediction created: {prediction.Id}");
    
    // ✅ INCREASED: 180 attempts = 6 minutes for 9-second videos
    return await PollPredictionAsync(prediction.Id, maxAttempts: 180);
}
    }
}
