import { describe, it, expect } from 'vitest';
import { renderMarkdown } from './markdown';

describe('renderMarkdown', () => {
	it('renders plain text as paragraph', () => {
		const html = renderMarkdown('hello');
		expect(html).toContain('<p>hello</p>');
	});

	it('renders bold markdown', () => {
		const html = renderMarkdown('**bold**');
		expect(html).toContain('<strong>bold</strong>');
	});

	it('renders code fences', () => {
		const html = renderMarkdown('```\ncode\n```');
		expect(html).toContain('<pre>');
		expect(html).toContain('<code>');
		expect(html).toContain('code');
	});

	it('renders inline code', () => {
		const html = renderMarkdown('`code`');
		expect(html).toContain('<code>code</code>');
	});

	it('sanitizes script tags', () => {
		const html = renderMarkdown("<script>alert('xss')</script>");
		expect(html).not.toContain('<script>');
	});

	it('sanitizes event handlers', () => {
		const html = renderMarkdown('<img onerror="alert(1)" src="x">');
		expect(html).not.toContain('onerror');
	});

	it('renders links', () => {
		const html = renderMarkdown('[click](https://example.com)');
		expect(html).toContain('<a');
		expect(html).toContain('href="https://example.com"');
	});

	it('renders unordered lists', () => {
		const html = renderMarkdown('- item 1\n- item 2');
		expect(html).toContain('<ul>');
		expect(html).toContain('<li>');
		expect(html).toContain('item 1');
	});

	it('renders headers', () => {
		const html = renderMarkdown('# Title');
		expect(html).toContain('<h1>');
		expect(html).toContain('Title');
	});
});
