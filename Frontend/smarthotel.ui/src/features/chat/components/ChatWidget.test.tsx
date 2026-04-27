import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { sendChatMessage } from '../api/chatApi'
import { ChatWidget } from './ChatWidget'

vi.mock('../api/chatApi', () => ({
  sendChatMessage: vi.fn(),
}))

describe('ChatWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('opens from launcher and sends typed message', async () => {
    const mockedSendChatMessage = vi.mocked(sendChatMessage)
    mockedSendChatMessage.mockResolvedValue({
      reply: 'Encontramos disponibilidad para esas fechas.',
      detectedLanguage: 'es',
      detectedIntent: 'consultar_disponibilidad',
    })

    const user = userEvent.setup()
    render(<ChatWidget />)

    await user.click(screen.getByRole('button', { name: /Abrir chat/i }))
    expect(screen.getByText(/Chatea con nosotros/i)).toBeInTheDocument()

    await user.type(screen.getByLabelText(/Escribe tu mensaje/i), 'Hay disponibilidad del 2026-06-10 al 2026-06-12?')
    await user.click(screen.getByRole('button', { name: /^Enviar$/i }))

    await waitFor(() => {
      expect(mockedSendChatMessage).toHaveBeenCalledWith('Hay disponibilidad del 2026-06-10 al 2026-06-12?')
    })

    expect(await screen.findByText(/Encontramos disponibilidad/i)).toBeInTheDocument()
  })

  it('uses quick suggestion chips', async () => {
    const mockedSendChatMessage = vi.mocked(sendChatMessage)
    mockedSendChatMessage.mockResolvedValue({
      reply: 'Tenemos gimnasio y sauna.',
      detectedLanguage: 'es',
      detectedIntent: 'consultar_servicios',
    })

    const user = userEvent.setup()
    render(<ChatWidget />)

    await user.click(screen.getByRole('button', { name: /Abrir chat/i }))
    await user.click(screen.getByRole('button', { name: /Servicios/i }))

    await waitFor(() => {
      expect(mockedSendChatMessage).toHaveBeenCalledWith('Que servicios tiene el hotel?')
    })

    expect(await screen.findByText(/gimnasio/i)).toBeInTheDocument()
  })
})
