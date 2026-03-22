# Agent Tools

The agent is a Semantic Kernel `ChatCompletionAgent` with auto function calling enabled. It has access to the following tools via `FileSystemPlugin`.

## Tool Inventory

### `ls`

Lists the contents of a directory within the workspace.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to list |

**Returns**: JSON array of objects with `name` (string), `length` (long, file size in bytes), and `isDirectory` (bool).

**Example return**:
```json
[
  { "name": "data.csv", "length": 1024, "isDirectory": false },
  { "name": "scripts", "length": 0, "isDirectory": true }
]
```

### `read_file`

Reads the content of a file within the workspace.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to file |
| `start` | int | No | Character offset to start reading from |
| `maxChars` | int | No | Maximum number of characters to read |

**Returns**: File content as string (full or partial).

### `write_file`

Creates or overwrites a file within the workspace.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path for the file |
| `content` | string | Yes | Content to write |

**Returns**: Confirmation message.

**Notes**: Automatically creates parent directories if they don't exist.

### `mkdir`

Creates a directory (and any parent directories) within the workspace. Succeeds silently if the directory already exists.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path of the directory to create |

**Returns**: Confirmation message.

### `rmdir`

Removes an empty directory from the workspace. Fails if the directory is not empty.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path of the directory to remove |

**Returns**: Confirmation message, or error message if directory is not found or not empty.

### `rm`

Removes a file from the workspace.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path of the file to remove |

**Returns**: Confirmation message, or error message if file is not found.

### `exec_script`

Executes an inline TypeScript script using the Deno runtime.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `scriptContent` | string | Yes | TypeScript code to execute |

**Returns**: JSON object with `exitCode` (int), `stdout` (string), and `stderr` (string).

**Description for agent**: Includes guidance that Deno uses `npm:` specifiers for npm packages (e.g., `import { DataFrame } from "npm:nodejs-polars"`) and `jsr:` for JSR packages.

### `exec_script_file`

Executes an existing TypeScript file from the workspace using the Deno runtime.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to the .ts file to execute |

**Returns**: JSON object with `exitCode` (int), `stdout` (string), and `stderr` (string).

### `response_include`

Reads a markdown file from the workspace and displays it as a rich content block directly in the conversation. Designed for showing large amounts of data to the user without cluttering the agent's text response.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to the .md file |

**Returns**: The raw markdown content.

**Special behavior**: The `WsToolCallFilter` intercepts this tool call and emits an additional `content_block` WebSocket message, causing the frontend to render the file as an inline document card with rendered markdown.

### `response_show_parquet`

Loads a Parquet file from the workspace and displays its contents as an interactive data table in the conversation. The user can download the data as CSV, Parquet, or XLSX.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to the .parquet file |

**Returns**: JSON with `columns` (array of {name, type}), `rows` (first 100 rows), `totalRowCount`, and `previewRowCount`.

**Special behavior**: The `WsToolCallFilter` intercepts this tool call and emits a `data_block` WebSocket message. The frontend renders it as a scrollable data table with download buttons for CSV, Parquet, and XLSX formats. Downloads are served via `GET /api/workspace/download?path={path}&format={csv|parquet|xlsx}`.

## Sandbox Security

All file operations are sandboxed to the workspace directory (`../workspace` relative to the backend, configurable via `Workspace:Root`).

### Path validation
- All paths are resolved against the workspace root
- Path traversal attempts (e.g., `../../etc/passwd`) are rejected
- Validation uses `Path.GetFullPath` + prefix check

### Deno permissions
- `--allow-read=<workspace>` — read files within workspace only
- `--allow-write=<workspace>` — write files within workspace only
- `--allow-import` — import from npm, jsr, and https://esm.sh
- No `--allow-net` — scripts cannot make HTTP requests
- No `--allow-env` — scripts cannot read environment variables
- No `--allow-run` — scripts cannot spawn subprocesses

## Agent Instructions

The agent receives the following system prompt:

```
You are a helpful assistant with access to a file system and a TypeScript/Deno runtime.
You can list directories (ls), read files (read_file), write files (write_file),
create directories (mkdir), remove directories (rmdir), remove files (rm),
and execute TypeScript scripts (exec_script, exec_script_file).
You can show parquet data to the user (response_show_parquet) and include markdown documents (response_include).

Always think step by step. When using tools, use precise relative paths.
After using tools, summarize what you found or did in clear language.
```
