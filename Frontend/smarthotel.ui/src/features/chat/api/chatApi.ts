import { httpRequest } from '../../../shared/api/httpClient';
import type { ChatMessageRequest, ChatMessageResponse } from '../types/chat';

export async function sendChatMessage(message: string): Promise<ChatMessageResponse> {
  const payload: ChatMessageRequest = { message };
  return httpRequest<ChatMessageResponse>('/api/chat/message', {
    method: 'POST',
    body: payload,
  });
}
