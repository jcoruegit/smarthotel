import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../shared/api/httpClient'
import { sendChatMessage } from '../api/chatApi'
import { useChat } from './useChat'

vi.mock('../api/chatApi', () => ({
  sendChatMessage: vi.fn(),
}))

describe('useChat', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('appends user and assistant messages on success', async () => {
    const mockedSendChatMessage = vi.mocked(sendChatMessage)
    mockedSendChatMessage.mockResolvedValue({
      reply: 'Tenemos gimnasio y sauna.',
      detectedLanguage: 'es',
      detectedIntent: 'consultar_servicios',
    })

    const { result } = renderHook(() => useChat())

    act(() => {
      result.current.setDraft('Que servicios tienen?')
    })

    await act(async () => {
      await result.current.handleSendDraft()
    })

    expect(mockedSendChatMessage).toHaveBeenCalledWith('Que servicios tienen?')
    expect(result.current.messages.some((message) => message.role === 'user' && message.text.includes('servicios'))).toBe(
      true,
    )
    expect(
      result.current.messages.some(
        (message) =>
          message.role === 'assistant' &&
          message.text.includes('gimnasio') &&
          message.detectedIntent === 'consultar_servicios',
      ),
    ).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('sets friendly error when API call fails', async () => {
    const mockedSendChatMessage = vi.mocked(sendChatMessage)
    mockedSendChatMessage.mockRejectedValue(new ApiError('Backend no disponible', 500))

    const { result } = renderHook(() => useChat())

    act(() => {
      result.current.setDraft('Hola')
    })

    await act(async () => {
      await result.current.handleSendDraft()
    })

    await waitFor(() => {
      expect(result.current.error).toBe('Backend no disponible')
    })
  })
})
