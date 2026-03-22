import { writable } from 'svelte/store';
import type { ClientMessage, ServerMessage } from '../types';

type WsStatus = 'connecting' | 'open' | 'closed' | 'error';

export const wsStatus = writable<WsStatus>('closed');

type MessageListener = (msg: ServerMessage) => void;
const listeners = new Set<MessageListener>();

let ws: WebSocket | null = null;
let reconnectAttempts = 0;
const MAX_ATTEMPTS = 5;

export function onMessage(fn: MessageListener): () => void {
	listeners.add(fn);
	return () => listeners.delete(fn);
}

export function send(msg: ClientMessage): void {
	if (ws?.readyState === WebSocket.OPEN) {
		ws.send(JSON.stringify(msg));
	}
}

export function connect(): void {
	if (ws?.readyState === WebSocket.OPEN || ws?.readyState === WebSocket.CONNECTING) return;

	const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
	const url = `${protocol}//${window.location.host}/ws`;

	wsStatus.set('connecting');
	ws = new WebSocket(url);

	ws.onopen = () => {
		wsStatus.set('open');
		reconnectAttempts = 0;
	};

	ws.onmessage = (event) => {
		try {
			const msg: ServerMessage = JSON.parse(event.data);
			listeners.forEach((fn) => fn(msg));
		} catch {
			// ignore malformed messages
		}
	};

	ws.onclose = () => {
		wsStatus.set('closed');
		ws = null;
		if (reconnectAttempts < MAX_ATTEMPTS) {
			const delay = Math.min(1000 * 2 ** reconnectAttempts, 30_000);
			reconnectAttempts++;
			setTimeout(connect, delay);
		}
	};

	ws.onerror = () => {
		wsStatus.set('error');
	};
}

export function disconnect(): void {
	ws?.close();
	ws = null;
}
