import { useCallback, useState } from 'react';
import { ApiError } from '../../../shared/api/httpClient';
import { sendChatMessage } from '../api/chatApi';
import type { ChatHistoryItem, ChatSuggestion, ChatRole } from '../types/chat';

const initialSuggestions: ChatSuggestion[] = [
  {
    id: 'availability',
    label: 'Disponibilidad',
    message: 'Hay disponibilidad del 2026-06-10 al 2026-06-12 para 2 huéspedes?',
  },
  {
    id: 'amenities',
    label: 'Servicios',
    message: 'Que servicios tiene el hotel?',
  },
  {
    id: 'schedules',
    label: 'Horarios',
    message: 'Cual es el horario de check-in, check-out y desayuno?',
  },
  {
    id: 'mixed',
    label: 'Consulta mixta',
    message: 'Hay disponibilidad del 2026-06-10 al 2026-06-12 y tambien sauna?',
  },
];

let messageSequence = 0;

function nextMessageId(role: ChatRole): string {
  messageSequence += 1;
  return `${role}-${Date.now()}-${messageSequence}`;
}

function createMessage(
  role: ChatRole,
  text: string,
  extras?: Pick<ChatHistoryItem, 'detectedIntent' | 'detectedLanguage'>,
): ChatHistoryItem {
  return {
    id: nextMessageId(role),
    role,
    text,
    createdAt: new Date().toISOString(),
    ...extras,
  };
}

export function useChat() {
  const [messages, setMessages] = useState<ChatHistoryItem[]>([
    createMessage(
      'assistant',
      'Hola, soy tu asistente del hotel. Puedo ayudarte con disponibilidad, servicios y horarios.',
    ),
  ]);
  const [draft, setDraft] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submitMessage = useCallback(async (rawMessage: string) => {
    const message = rawMessage.trim();
    if (!message || loading) {
      return;
    }

    setError(null);
    setLoading(true);
    setMessages((current) => [...current, createMessage('user', message)]);

    try {
      const response = await sendChatMessage(message);
      setMessages((current) => [
        ...current,
        createMessage('assistant', response.reply, {
          detectedIntent: response.detectedIntent,
          detectedLanguage: response.detectedLanguage,
        }),
      ]);
    } catch (unknownError) {
      const messageFromError =
        unknownError instanceof ApiError ? unknownError.message : 'No pudimos obtener respuesta del asistente.';
      setError(messageFromError);
    } finally {
      setLoading(false);
    }
  }, [loading]);

  const handleSendDraft = useCallback(async () => {
    const message = draft.trim();
    if (!message) {
      return;
    }

    setDraft('');
    await submitMessage(message);
  }, [draft, submitMessage]);

  const handleSuggestionClick = useCallback(async (suggestion: ChatSuggestion) => {
    setDraft('');
    await submitMessage(suggestion.message);
  }, [submitMessage]);

  return {
    messages,
    draft,
    setDraft,
    loading,
    error,
    suggestions: initialSuggestions,
    handleSendDraft,
    handleSuggestionClick,
  };
}
