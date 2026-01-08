using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// Manages chat interactions with the Google Gemini API.
/// Provides a UI-based chat interface using TextMeshPro components.
/// </summary>
public class GeminiChatManager : MonoBehaviour
{
    #region Data Structures

    [Serializable]
    private class GeminiRequest
    {
        public List<Content> contents;
        public Content system_instruction;
        public GenerationConfig generationConfig;
    }

    [Serializable]
    private class Content
    {
        public string role;
        public List<Part> parts;
    }

    [Serializable]
    private class Part
    {
        public string text;
    }

    [Serializable]
    private class GenerationConfig
    {
        public float temperature = 1.0f;
        public int maxOutputTokens = 8192;
    }

    [Serializable]
    private class GeminiResponse
    {
        public List<Candidate> candidates;
        public UsageMetadata usageMetadata;
        public string error;
    }

    [Serializable]
    private class Candidate
    {
        public Content content;
        public string finishReason;
    }

    [Serializable]
    private class UsageMetadata
    {
        public int promptTokenCount;
        public int candidatesTokenCount;
        public int totalTokenCount;
    }

    [Serializable]
    private class ErrorResponse
    {
        public Error error;
    }

    [Serializable]
    private class Error
    {
        public int code;
        public string message;
        public string status;
    }

    #endregion

    #region Inspector Fields

    [Header("API Configuration")]
    [Tooltip("Your Google Gemini API key. Get one at https://aistudio.google.com/app/apikey")]
    [SerializeField] private string apiKey = "";

    [Tooltip("The Gemini model to use")]
    [SerializeField] private string modelName = "gemini-3-flash-preview";

    [Header("System Prompt")]
    [Tooltip("System instructions that define the AI's behavior and personality")]
    [TextArea(5, 15)]
    [SerializeField] private string systemPrompt = "You are a helpful AI assistant.";

    [Header("UI Elements")]
    [Tooltip("TextMeshPro text component to display the conversation")]
    [SerializeField] private TMP_Text outputText;

    [Tooltip("TextMeshPro input field for user messages")]
    [SerializeField] private TMP_InputField inputField;

    [Header("Settings")]
    [Tooltip("Temperature for response generation (0.0 - 2.0). Higher = more creative")]
    [Range(0f, 2f)]
    [SerializeField] private float temperature = 1.0f;

    [Tooltip("Maximum tokens in the response")]
    [SerializeField] private int maxOutputTokens = 8192;

    [Tooltip("Keep conversation history for context")]
    [SerializeField] private bool maintainHistory = true;

    [Tooltip("Maximum number of conversation turns to keep in history")]
    [SerializeField] private int maxHistoryTurns = 10;

    [Tooltip("Speed of typewriter effect in seconds per character")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float typingSpeed = 0.02f;

    #endregion

    #region Private Fields

    private const string API_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models/";
    private List<Content> conversationHistory = new List<Content>();
    private bool isProcessing = false;
    private Coroutine currentTypingCoroutine = null;
    
    // Emotion and gesture state
    private string currentEmotion = "neutral";
    private string currentGesture = "standing";
    private bool isTyping = false;
    
    // Format instructions added to every system prompt
    private const string FORMAT_INSTRUCTIONS = "\n\nIMPORTANT FORMATTING RULES:\n1. Keep your response to 1-3 sentences maximum.\n2. Start your response with a character description in brackets fitting for their latest response that ALWAYS begins with 'the character is' followed by their emotion and action.\n3. Use vivid, expressive emotions and dramatic physical actions that can be clearly conveyed without sound.\n4. Example formats:\n[the character is laughing hysterically and clapping his hands]\n[the character is terrified and backing away with hands raised]\n[the character is ecstatic and jumping up and down]\n[the character is devastated and collapsing to his knees]\n5. Be creative and dramatic with emotions and actions - make them BIG and VISUAL!";

    #endregion
    
    #region Events
    
    /// <summary>
    /// Called when the AI starts typing a response. Provides emotion, gesture.
    /// </summary>
    public event System.Action<string, string> OnTypingStarted;
    
    /// <summary>
    /// Called when the AI finishes typing a response.
    /// </summary>
    public event System.Action OnTypingFinished;
    
    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        ValidateConfiguration();
        
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnInputSubmit);
        }

        if (outputText != null)
        {
            outputText.text = "Gemini Chat Ready. Type a message and press Enter.";
        }
    }

    private void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(OnInputSubmit);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Send a message to Gemini and get a response.
    /// </summary>
    /// <param name="userMessage">The message to send</param>
    public void SendMessage(string userMessage)
    {
        if (string.IsNullOrEmpty(userMessage))
        {
            Debug.LogWarning("[Gemini] Cannot send empty message");
            return;
        }

        if (isProcessing)
        {
            Debug.LogWarning("[Gemini] Already processing a request");
            return;
        }

        StartCoroutine(SendMessageCoroutine(userMessage));
    }

    /// <summary>
    /// Clear the conversation history and display.
    /// </summary>
    public void ClearConversation()
    {
        conversationHistory.Clear();
        
        if (outputText != null)
        {
            outputText.text = "";
        }
        
        Debug.Log("[Gemini] Conversation cleared");
    }

    /// <summary>
    /// Update the system prompt at runtime.
    /// </summary>
    /// <param name="newPrompt">The new system prompt</param>
    public void SetSystemPrompt(string newPrompt)
    {
        systemPrompt = newPrompt;
        Debug.Log("[Gemini] System prompt updated");
    }

    /// <summary>
    /// Set the Gemini API key at runtime.
    /// </summary>
    /// <param name="newApiKey">The new API key</param>
    public void SetApiKey(string newApiKey)
    {
        if (string.IsNullOrEmpty(newApiKey))
        {
            Debug.LogWarning("[Gemini] Cannot set empty API key.");
            return;
        }
        
        apiKey = newApiKey;
        Debug.Log("[Gemini] API key updated.");
    }
    
    /// <summary>
    /// Get the current emotion parsed from the last response.
    /// </summary>
    public string CurrentEmotion => currentEmotion;
    
    /// <summary>
    /// Get the current gesture parsed from the last response.
    /// </summary>
    public string CurrentGesture => currentGesture;
    
    /// <summary>
    /// Check if the AI is currently typing a response.
    /// </summary>
    public bool IsTyping => isTyping;

    /// <summary>
    /// Send a greeting request to have the AI introduce itself.
    /// </summary>
    public void SendGreeting()
    {
        if (isProcessing)
        {
            Debug.LogWarning("[Gemini] Already processing a request, cannot send greeting");
            return;
        }

        // Send a hidden prompt that asks the AI to introduce itself
        StartCoroutine(SendGreetingCoroutine());
    }

    private IEnumerator SendGreetingCoroutine()
    {
        isProcessing = true;
        
        // Show loading state
        if (outputText != null)
        {
            outputText.text = "...";
        }

        // Create a greeting request - the AI will introduce itself based on its system prompt
        var greetingContent = new Content
        {
            role = "user",
            parts = new List<Part> { new Part { text = "Introduce yourself in one sentence." } }
        };

        // Build request (don't add to history - this is a hidden prompt)
        var request = new GeminiRequest
        {
            contents = new List<Content> { greetingContent },
            generationConfig = new GenerationConfig
            {
                temperature = temperature,
                maxOutputTokens = 500 // Generous limit for greeting with tag
            }
        };

        // Add system instruction if provided (with format instructions)
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.system_instruction = new Content
            {
                parts = new List<Part> { new Part { text = systemPrompt + FORMAT_INSTRUCTIONS } }
            };
        }
        else
        {
            request.system_instruction = new Content
            {
                parts = new List<Part> { new Part { text = FORMAT_INSTRUCTIONS } }
            };
        }

        string jsonBody = JsonUtility.ToJson(request);
        string url = $"{API_BASE_URL}{modelName}:generateContent?key={apiKey}";

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[Gemini] Sending greeting request...");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[Gemini] Greeting request failed: {webRequest.error}");
                if (outputText != null)
                {
                    outputText.text = "Hello! How can I help you?";
                }
            }
            else
            {
                try
                {
                    var response = JsonUtility.FromJson<GeminiResponse>(webRequest.downloadHandler.text);
                    
                    if (response.candidates != null && response.candidates.Count > 0 &&
                        response.candidates[0].content?.parts != null &&
                        response.candidates[0].content.parts.Count > 0)
                    {
                        string rawGreeting = response.candidates[0].content.parts[0].text;
                        Debug.Log($"[Gemini] Raw greeting received (length {rawGreeting?.Length ?? 0}): '{rawGreeting}'");
                        
                        // Parse emotion and gesture from greeting
                        string greetingMessage = ParseEmotionAndGesture(rawGreeting);
                        Debug.Log($"[Gemini] Parsed greeting message (length {greetingMessage?.Length ?? 0}): '{greetingMessage}'");
                        Debug.Log($"[Gemini] Current emotion: '{currentEmotion}', gesture: '{currentGesture}'");
                        
                        // If parsing resulted in empty message (e.g., truncated response), use a default greeting
                        if (string.IsNullOrWhiteSpace(greetingMessage))
                        {
                            Debug.LogWarning($"[Gemini] Greeting parsing resulted in empty text. Response may have been truncated.");
                            greetingMessage = "Hello! Welcome, how can I help you today?";
                            currentEmotion = "friendly";
                            currentGesture = "waving";
                        }

                        // Add the greeting to history (store the display message, not the raw one with tags)
                        if (maintainHistory)
                        {
                            conversationHistory.Add(new Content
                            {
                                role = "model",
                                parts = new List<Part> { new Part { text = greetingMessage } }
                            });
                        }

                        // Display with typewriter effect
                        if (outputText != null)
                        {
                            if (currentTypingCoroutine != null)
                            {
                                StopCoroutine(currentTypingCoroutine);
                            }
                            currentTypingCoroutine = StartCoroutine(TypewriterEffect(greetingMessage));
                        }
                    }
                    else
                    {
                        currentEmotion = "neutral";
                        currentGesture = "standing";
                        if (outputText != null)
                        {
                            isTyping = true;
                            OnTypingStarted?.Invoke(currentEmotion, currentGesture);
                            outputText.text = "Hello! How can I help you?";
                            isTyping = false;
                            OnTypingFinished?.Invoke();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Gemini] Failed to parse greeting response: {e.Message}");
                    currentEmotion = "neutral";
                    currentGesture = "standing";
                    if (outputText != null)
                    {
                        isTyping = true;
                        OnTypingStarted?.Invoke(currentEmotion, currentGesture);
                        outputText.text = "Hello! How can I help you?";
                        isTyping = false;
                        OnTypingFinished?.Invoke();
                    }
                }
            }
        }

        isProcessing = false;
    }

    #endregion

    #region Private Methods

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[Gemini] API key is not set! Please enter your API key in the inspector.");
        }

        if (outputText == null)
        {
            Debug.LogWarning("[Gemini] Output text is not assigned. Chat responses won't be displayed.");
        }

        if (inputField == null)
        {
            Debug.LogWarning("[Gemini] Input field is not assigned. Use SendMessage() method to send messages.");
        }
    }

    private void OnInputSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        SendMessage(text);
        inputField.text = "";
        inputField.ActivateInputField();
    }

    /// <summary>
    /// Sends the message from the input field. Call this from a UI button's OnClick event.
    /// </summary>
    public void SendInputFieldMessage()
    {
        if (inputField == null)
        {
            Debug.LogWarning("[Gemini] Input field is not assigned.");
            return;
        }

        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;
        
        SendMessage(text);
        inputField.text = "";
        inputField.ActivateInputField();
    }

    private IEnumerator SendMessageCoroutine(string userMessage)
    {
        isProcessing = true;
        
        // Show loading state
        if (outputText != null)
        {
            outputText.text = "Thinking...";
        }

        // Add user message to history
        var userContent = new Content
        {
            role = "user",
            parts = new List<Part> { new Part { text = userMessage } }
        };

        if (maintainHistory)
        {
            conversationHistory.Add(userContent);
            TrimHistory();
        }

        // Build request
        var request = new GeminiRequest
        {
            contents = maintainHistory ? conversationHistory : new List<Content> { userContent },
            generationConfig = new GenerationConfig
            {
                temperature = temperature,
                maxOutputTokens = maxOutputTokens
            }
        };

        // Add system instruction if provided (with format instructions appended)
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.system_instruction = new Content
            {
                parts = new List<Part> { new Part { text = systemPrompt + FORMAT_INSTRUCTIONS } }
            };
        }
        else
        {
            request.system_instruction = new Content
            {
                parts = new List<Part> { new Part { text = FORMAT_INSTRUCTIONS } }
            };
        }

        string jsonBody = JsonUtility.ToJson(request);
        string url = $"{API_BASE_URL}{modelName}:generateContent?key={apiKey}";

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[Gemini] Sending request to {modelName}...");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMessage = $"Error: {webRequest.error}";
                
                // Try to parse error response for more details
                try
                {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(webRequest.downloadHandler.text);
                    if (errorResponse?.error != null)
                    {
                        errorMessage = $"Error ({errorResponse.error.code}): {errorResponse.error.message}";
                    }
                }
                catch { }

                Debug.LogError($"[Gemini] {errorMessage}");
                
                // Display error in output
                if (outputText != null)
                {
                    outputText.text = $"Error: {errorMessage}";
                }

                // Remove the failed user message from history
                if (maintainHistory && conversationHistory.Count > 0)
                {
                    conversationHistory.RemoveAt(conversationHistory.Count - 1);
                }
            }
            else
            {
                string responseText = webRequest.downloadHandler.text;
                Debug.Log($"[Gemini] Response received: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

                try
                {
                    var response = JsonUtility.FromJson<GeminiResponse>(responseText);
                    
                    if (response.candidates != null && response.candidates.Count > 0 &&
                        response.candidates[0].content?.parts != null &&
                        response.candidates[0].content.parts.Count > 0)
                    {
                        string rawMessage = response.candidates[0].content.parts[0].text;
                        
                        // Parse emotion and gesture from response
                        string displayMessage = ParseEmotionAndGesture(rawMessage);

                        // Add assistant response to history (with parsed message)
                        if (maintainHistory)
                        {
                            conversationHistory.Add(new Content
                            {
                                role = "model",
                                parts = new List<Part> { new Part { text = displayMessage } }
                            });
                        }

                        // Display the assistant's response with typewriter effect
                        if (outputText != null)
                        {
                            if (currentTypingCoroutine != null)
                            {
                                StopCoroutine(currentTypingCoroutine);
                            }
                            currentTypingCoroutine = StartCoroutine(TypewriterEffect(displayMessage));
                        }

                        if (response.usageMetadata != null)
                        {
                            Debug.Log($"[Gemini] Tokens used - Prompt: {response.usageMetadata.promptTokenCount}, Response: {response.usageMetadata.candidatesTokenCount}, Total: {response.usageMetadata.totalTokenCount}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Gemini] Response contained no valid candidates");
                        if (outputText != null)
                        {
                            outputText.text = "No response generated. The model may have filtered the response.";
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Gemini] Failed to parse response: {e.Message}\nResponse: {responseText}");
                    if (outputText != null)
                    {
                        outputText.text = "Failed to parse response";
                    }
                }
            }
        }

        isProcessing = false;
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        Debug.Log($"[Gemini] TypewriterEffect starting with text: '{fullText}' (length: {fullText?.Length ?? 0})");
        outputText.text = "";
        isTyping = true;
        
        // Notify listeners that typing started with current emotion and gesture
        OnTypingStarted?.Invoke(currentEmotion, currentGesture);
        
        foreach (char c in fullText)
        {
            outputText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        
        Debug.Log($"[Gemini] TypewriterEffect finished. Final text length: {outputText.text?.Length ?? 0}");
        isTyping = false;
        currentTypingCoroutine = null;
        
        // Notify listeners that typing finished
        OnTypingFinished?.Invoke();
    }
    
    /// <summary>
    /// Parse emotion and gesture from the AI response.
    /// Expected format: [the character is {description}] followed by the message.
    /// </summary>
    private string ParseEmotionAndGesture(string rawMessage)
    {
        if (string.IsNullOrEmpty(rawMessage))
            return rawMessage;
        
        string trimmedMessage = rawMessage.Trim();
        Debug.Log($"[Gemini] ParseEmotionAndGesture input: '{trimmedMessage}'");
        
        // Try to find any bracketed text at the start: [anything]
        var bracketMatch = System.Text.RegularExpressions.Regex.Match(
            trimmedMessage, 
            @"^\[([^\]]+)\]\s*(.*)$",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        
        if (bracketMatch.Success)
        {
            string bracketContent = bracketMatch.Groups[1].Value.Trim();
            string messageText = bracketMatch.Groups[2].Value.Trim();
            
            Debug.Log($"[Gemini] Found bracket content: '{bracketContent}'");
            Debug.Log($"[Gemini] Message after bracket: '{messageText}'");
            
            // Check if it contains "the character is" and extract description
            var characterMatch = System.Text.RegularExpressions.Regex.Match(
                bracketContent,
                @"the character is\s+(.+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            
            if (characterMatch.Success)
            {
                string description = characterMatch.Groups[1].Value.Trim();
                ParseDescriptionIntoEmotionAndGesture(description);
                Debug.Log($"[Gemini] Parsed description: '{description}'");
            }
            else
            {
                // Use the entire bracket content as description
                ParseDescriptionIntoEmotionAndGesture(bracketContent);
                Debug.Log($"[Gemini] Using full bracket as description: '{bracketContent}'");
            }
            
            Debug.Log($"[Gemini] Emotion: '{currentEmotion}', Gesture: '{currentGesture}'");
            
            // Return the message text (may be empty if AI only sent the tag)
            return messageText;
        }
        
        // Check if response starts with '[' but has no closing ']' (truncated response)
        if (trimmedMessage.StartsWith("[") && !trimmedMessage.Contains("]"))
        {
            Debug.LogWarning($"[Gemini] Response appears truncated (no closing bracket): '{trimmedMessage}'");
            currentEmotion = "neutral";
            currentGesture = "standing";
            // Return empty - caller should handle this as a failed parse
            return "";
        }
        
        // Check if there's a bracket somewhere in the message that we should strip
        var anyBracketMatch = System.Text.RegularExpressions.Regex.Match(
            trimmedMessage,
            @"\[[^\]]*\]\s*",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        
        if (anyBracketMatch.Success)
        {
            // Strip the bracket content and return rest
            string cleaned = System.Text.RegularExpressions.Regex.Replace(trimmedMessage, @"\[[^\]]*\]\s*", "").Trim();
            Debug.Log($"[Gemini] Stripped mid-message bracket, result: '{cleaned}'");
            currentEmotion = "neutral";
            currentGesture = "standing";
            return cleaned;
        }
        
        // No bracketed tags found, return original message
        Debug.LogWarning($"[Gemini] No bracketed tag found in response: '{trimmedMessage}'");
        currentEmotion = "neutral";
        currentGesture = "standing";
        return rawMessage;
    }
    
    /// <summary>
    /// Parse a natural language description into emotion and gesture components.
    /// Example: "laughing and clapping his hands" -> emotion: laughing, gesture: clapping_his_hands
    /// </summary>
    private void ParseDescriptionIntoEmotionAndGesture(string description)
    {
        // Split by "and" to try to separate emotion from action
        var parts = description.Split(new[] { " and ", " while ", ", " }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 2)
        {
            currentEmotion = parts[0].Trim().ToLower();
            currentGesture = parts[1].Trim().ToLower().Replace(" ", "_");
        }
        else if (parts.Length == 1)
        {
            // Only one part - could be just emotion or just gesture
            string part = parts[0].Trim().ToLower();
            
            // Common emotion words
            string[] emotionWords = { "happy", "sad", "angry", "laughing", "ecstatic", "furious", "terrified", 
                                     "devastated", "heartbroken", "overjoyed", "panicked", "disgusted", "shocked",
                                     "horrified", "thrilled", "enraged", "euphoric", "hysterical", "melancholic",
                                     "desperate", "triumphant", "manic", "betrayed", "awestruck", "mortified" };
            
            bool isEmotion = false;
            foreach (var emotion in emotionWords)
            {
                if (part.Contains(emotion))
                {
                    isEmotion = true;
                    break;
                }
            }
            
            if (isEmotion)
            {
                currentEmotion = part;
                currentGesture = "standing";
            }
            else
            {
                currentEmotion = "neutral";
                currentGesture = part.Replace(" ", "_");
            }
        }
        else
        {
            currentEmotion = "neutral";
            currentGesture = "standing";
        }
    }

    private void TrimHistory()
    {
        // Each turn has 2 messages (user + assistant), so multiply maxHistoryTurns by 2
        int maxMessages = maxHistoryTurns * 2;
        
        while (conversationHistory.Count > maxMessages)
        {
            conversationHistory.RemoveAt(0);
        }
    }

    #endregion

    #region Context Menu

    [ContextMenu("Test Send Message")]
    private void TestSendMessage()
    {
        SendMessage("Hello! Can you tell me a short joke?");
    }

    [ContextMenu("Clear Conversation")]
    private void ClearConversationMenu()
    {
        ClearConversation();
    }

    #endregion
}
