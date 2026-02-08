#!/usr/bin/env node

/**
 * build-ssr-css.js
 *
 * Compiles Angular widget SCSS files into a single CSS file for server-side
 * rendering.  This ensures the SSR output uses the **exact same styles** as
 * the Angular application — the component SCSS files are the single source
 * of truth.
 *
 * Pipeline:
 *   1. Read SSR-specific base styles              (src/ssr-base.css)
 *   2. Read widget-preview component SCSS          (widget-preview.component.scss)
 *   3. Read every widget *.component.scss          (widgets/*.component.scss)
 *   4. Apply SSR transformations:
 *        :host       → mapped wrapper class  (e.g. .app-icon-host)
 *        :deep(sel)  → sel                   (remove Vue-style deep combinator)
 *   5. Compile each SCSS source to CSS via `sass`
 *   6. Concatenate with section headers
 *   7. Write combined output to  ../wwwroot/css/ssr-widgets.css
 *
 * Usage:  node build-ssr-css.js
 */

const fs   = require('fs');
const path = require('path');
const sass = require('sass');

// ──────────── paths ────────────

const WIDGETS_DIR        = path.join(__dirname, 'src/app/components/widgets');
const WIDGET_PREVIEW     = path.join(__dirname, 'src/app/components/widget-preview/widget-preview.component.scss');
const SSR_BASE_CSS       = path.join(__dirname, 'src/ssr-base.css');
const OUTPUT_FILE        = path.join(__dirname, '../wwwroot/css/ssr-widgets.css');

// ──────────── :host class map ────────────
// When Angular's :host is used, map it to the SSR wrapper class emitted by
// DashboardHtmlRenderingService.

const HOST_CLASS_MAP = {
  'app-icon-widget.component.scss': '.app-icon-host',
};

// ──────────── helpers ────────────

function getWidgetScssFiles() {
  return fs.readdirSync(WIDGETS_DIR)
    .filter(f => f.endsWith('.component.scss'))
    .sort()
    .map(f => path.join(WIDGETS_DIR, f));
}

/** Apply SSR-specific transformations to raw SCSS source. */
function transformScss(scss, fileName) {
  let out = scss;

  // :host  →  SSR wrapper class
  const hostClass = HOST_CLASS_MAP[fileName];
  if (hostClass) {
    out = out.replace(/:host\b/g, hostClass);
  }

  // :deep(selector)  →  selector   (Vue-style syntax used in a few files)
  out = out.replace(/:deep\(([^)]+)\)/g, '$1');

  return out;
}

/** Compile an SCSS string to CSS. */
function compileScss(scss, sourceFile) {
  try {
    return sass.compileString(scss, {
      style: 'expanded',
      loadPaths: [path.dirname(sourceFile)],
    }).css;
  } catch (err) {
    console.error(`✗ Error compiling ${sourceFile}:\n  ${err.message}`);
    process.exit(1);
  }
}

/** Turn a file name into a section header: "calendar-widget" → "CALENDAR WIDGET" */
function sectionName(filePath) {
  return path.basename(filePath)
    .replace('.component.scss', '')
    .replace(/-/g, ' ')
    .toUpperCase();
}

// ──────────── main ────────────

function main() {
  console.log('Building SSR widget CSS …');

  const parts = [];

  // 1. SSR-specific base styles ──────────────────────────────────────────
  if (!fs.existsSync(SSR_BASE_CSS)) {
    console.error(`✗ SSR base CSS not found: ${SSR_BASE_CSS}`);
    process.exit(1);
  }
  parts.push(fs.readFileSync(SSR_BASE_CSS, 'utf-8'));

  // 2. Widget-preview component ──────────────────────────────────────────
  if (fs.existsSync(WIDGET_PREVIEW)) {
    const scss = transformScss(fs.readFileSync(WIDGET_PREVIEW, 'utf-8'), path.basename(WIDGET_PREVIEW));
    parts.push(`/* ===== WIDGET PREVIEW ===== */\n${compileScss(scss, WIDGET_PREVIEW)}`);
  }

  // 3. All widget component SCSS files ───────────────────────────────────
  const widgetFiles = getWidgetScssFiles();
  for (const filePath of widgetFiles) {
    const fileName = path.basename(filePath);
    const scss     = transformScss(fs.readFileSync(filePath, 'utf-8'), fileName);
    const css      = compileScss(scss, filePath);
    parts.push(`/* ===== ${sectionName(filePath)} ===== */\n${css}`);
  }

  // 4. Assemble final output ─────────────────────────────────────────────
  const header = [
    '/*',
    ' * SSR Widget Styles — AUTO-GENERATED',
    ' * ====================================',
    ' * Compiled from the Angular component SCSS files by build-ssr-css.js.',
    ' * Do NOT edit manually — changes will be overwritten on the next build.',
    ' *',
    ` * Generated: ${new Date().toISOString()}`,
    ` * Sources:   ${widgetFiles.length} widget SCSS + widget-preview + ssr-base.css`,
    ' */',
    '',
  ].join('\n');

  const output = header + parts.join('\n\n') + '\n';

  // Ensure output directory exists
  fs.mkdirSync(path.dirname(OUTPUT_FILE), { recursive: true });
  fs.writeFileSync(OUTPUT_FILE, output, 'utf-8');

  console.log(`✓ SSR CSS written to ${path.relative(process.cwd(), OUTPUT_FILE)}`);
  console.log(`  ${output.length} bytes, ${widgetFiles.length + 1} component files compiled`);
}

main();
