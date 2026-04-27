import { describe, expect, it, vi, beforeEach } from 'vitest'
import { sendChatMessage } from './chatApi'
import { httpRequest } from '../../../shared/api/httpClient'

vi.mock('../../../shared/api/httpClient', () => ({
  httpRequest: vi.fn(),
}))

describe('chatApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('sends message using POST /api/chat/message', async () => {
    const mockedHttpRequest = vi.mocked(httpRequest)
    mockedHttpRequest.mockResolvedValue({
      reply: 'ok',
      detectedLanguage: 'es',
      detectedIntent: 'fallback',
    })

    const response = await sendChatMessage('Hola')

    expect(response.reply).toBe('ok')
    expect(mockedHttpRequest).toHaveBeenCalledTimes(1)
    expect(mockedHttpRequest).toHaveBeenCalledWith('/api/chat/message', {
      method: 'POST',
      body: { message: 'Hola' },
    })
  })
})
