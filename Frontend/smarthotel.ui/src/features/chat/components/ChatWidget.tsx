import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useChat } from '../hooks/useChat';

export function ChatWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const { messages, draft, setDraft, loading, error, suggestions, handleSendDraft, handleSuggestionClick } = useChat();
  const listRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!isOpen || !listRef.current) {
      return;
    }

    listRef.current.scrollTop = listRef.current.scrollHeight;
  }, [isOpen, loading, messages]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await handleSendDraft();
  }

  return (
    <aside className="chat-widget" aria-label="Asistente de chat">
      {isOpen ? (
        <section className="chat-widget-panel">
          <header className="chat-widget-header">
            <div>
              <h2>Chatea con nosotros</h2>
              <p>Reservas, servicios y horarios en segundos.</p>
            </div>

            <button
              className="chat-widget-close"
              type="button"
              aria-label="Cerrar chat"
              onClick={() => setIsOpen(false)}
            >
              x
            </button>
          </header>

          <div className="chat-widget-suggestions">
            {suggestions.map((suggestion) => (
              <button
                key={suggestion.id}
                className="chat-widget-chip"
                type="button"
                disabled={loading}
                onClick={() => {
                  void handleSuggestionClick(suggestion);
                }}
              >
                {suggestion.label}
              </button>
            ))}
          </div>

          <div className="chat-widget-messages" ref={listRef}>
            {messages.map((message) => (
              <article
                key={message.id}
                className={`chat-widget-bubble ${message.role === 'user' ? 'is-user' : 'is-assistant'}`}
              >
                <p>{message.text}</p>
              </article>
            ))}

            {loading ? <p className="chat-widget-status">Respondiendo...</p> : null}
          </div>

          <form className="chat-widget-form" onSubmit={handleSubmit}>
            <textarea
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              rows={2}
              placeholder="Escribe tu mensaje..."
              disabled={loading}
              aria-label="Escribe tu mensaje"
            />

            <button className="chat-widget-send" type="submit" disabled={loading || !draft.trim()}>
              Enviar
            </button>
          </form>

          {error ? <p className="message error chat-widget-error">{error}</p> : null}
        </section>
      ) : null}

      <button
        className="chat-widget-launcher"
        type="button"
        aria-label={isOpen ? 'Minimizar chat' : 'Abrir chat'}
        onClick={() => setIsOpen((current) => !current)}
      >
        {isOpen ? 'Minimizar chat' : 'Chatea con nosotros'}
      </button>
    </aside>
  );
}
