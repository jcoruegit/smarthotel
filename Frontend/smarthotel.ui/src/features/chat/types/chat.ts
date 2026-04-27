export type ChatRole = 'user' | 'assistant';

export interface ChatMessageRequest {
  message: string;
}

export interface ChatMessageResponse {
  reply: string;
  detectedLanguage: string;
  detectedIntent: string;
}

export interface ChatHistoryItem {
  id: string;
  role: ChatRole;
  text: string;
  detectedIntent?: string;
  detectedLanguage?: string;
  createdAt: string;
}

export interface ChatSuggestion {
  id: string;
  label: string;
  message: string;
}
