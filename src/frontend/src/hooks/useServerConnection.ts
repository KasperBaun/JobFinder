import { useEffect, useRef, useState } from 'react'
import { ping } from '../api/client'

type ConnectionStatus = 'connected' | 'disconnected'

const PING_INTERVAL = 5_000
const FAILURE_THRESHOLD = 2

export function useServerConnection(): ConnectionStatus {
  const [status, setStatus] = useState<ConnectionStatus>('connected')
  const failCountRef = useRef(0)

  useEffect(() => {
    const id = setInterval(async () => {
      try {
        await ping()
        failCountRef.current = 0
        setStatus('connected')
      } catch {
        failCountRef.current++
        if (failCountRef.current >= FAILURE_THRESHOLD) {
          setStatus('disconnected')
        }
      }
    }, PING_INTERVAL)

    return () => clearInterval(id)
  }, [])

  return status
}
