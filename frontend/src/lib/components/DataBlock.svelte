<script lang="ts">
	let {
		source,
		columns,
		rows,
		totalRowCount,
		previewRowCount
	}: {
		source: string;
		columns: Array<{ name: string; type: string }>;
		rows: Array<Array<unknown>>;
		totalRowCount: number;
		previewRowCount: number;
	} = $props();

	function downloadUrl(format: string): string {
		return `/api/workspace/download?path=${encodeURIComponent(source)}&format=${format}`;
	}

	function isNumeric(type: string): boolean {
		return type === 'int' || type === 'double';
	}

	function formatValue(value: unknown): string {
		if (value === null || value === undefined) return '';
		return String(value);
	}
</script>

<div class="my-2 rounded-xl border border-slate-200 bg-slate-50 overflow-hidden shadow-sm">
	<div class="flex items-center gap-2 px-3 py-2 bg-slate-100 border-b border-slate-200">
		<span class="text-slate-400 text-xs">📊</span>
		<span class="text-xs font-mono text-slate-600 truncate">{source}</span>
		<span class="ml-auto text-xs text-slate-400 shrink-0">
			{#if previewRowCount < totalRowCount}
				Showing {previewRowCount} of {totalRowCount.toLocaleString()} rows
			{:else}
				{totalRowCount.toLocaleString()} rows
			{/if}
		</span>
	</div>

	<div class="flex gap-2 px-3 py-2 border-b border-slate-200 bg-slate-50">
		<a
			href={downloadUrl('csv')}
			download
			class="inline-flex items-center gap-1 px-2 py-1 rounded text-xs font-medium
			       bg-white border border-slate-200 text-slate-600 hover:bg-slate-100 hover:text-slate-800 transition-colors"
		>
			CSV
		</a>
		<a
			href={downloadUrl('parquet')}
			download
			class="inline-flex items-center gap-1 px-2 py-1 rounded text-xs font-medium
			       bg-white border border-slate-200 text-slate-600 hover:bg-slate-100 hover:text-slate-800 transition-colors"
		>
			Parquet
		</a>
		<a
			href={downloadUrl('xlsx')}
			download
			class="inline-flex items-center gap-1 px-2 py-1 rounded text-xs font-medium
			       bg-white border border-slate-200 text-slate-600 hover:bg-slate-100 hover:text-slate-800 transition-colors"
		>
			XLSX
		</a>
	</div>

	<div class="overflow-x-auto overflow-y-auto max-h-96">
		<table class="w-full text-xs font-mono border-collapse">
			<thead>
				<tr class="bg-slate-100 sticky top-0">
					{#each columns as col}
						<th
							class="px-3 py-1.5 text-left font-semibold text-slate-700 border-b border-slate-200 whitespace-nowrap
							       {isNumeric(col.type) ? 'text-right' : ''}"
						>
							{col.name}
							<span class="font-normal text-slate-400 ml-1">{col.type}</span>
						</th>
					{/each}
				</tr>
			</thead>
			<tbody>
				{#each rows as row, i}
					<tr class="{i % 2 === 0 ? 'bg-white' : 'bg-slate-50/50'} hover:bg-blue-50 transition-colors">
						{#each row as value, c}
							<td
								class="px-3 py-1 border-b border-slate-100 whitespace-nowrap
								       {isNumeric(columns[c].type) ? 'text-right' : ''}
								       {value === null || value === undefined ? 'text-slate-300 italic' : 'text-slate-700'}"
							>
								{value === null || value === undefined ? 'null' : formatValue(value)}
							</td>
						{/each}
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
</div>
