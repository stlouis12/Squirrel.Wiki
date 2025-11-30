# Table of Contents Plugin

A Markdown extension plugin for Squirrel Wiki that automatically generates a table of contents from page headings.

## Features

- **Automatic TOC Generation**: Simply add `{{toc}}` anywhere in your markdown content
- **Configurable Depth**: Control which heading levels to include (H1-H6)
- **Nested Structure**: Properly indented TOC with nested lists for all heading levels
- **Anchor Links**: Clickable links that jump to each heading
- **Semantic HTML**: Uses proper `<nav>` element with ARIA roles

## Installation

1. Build the plugin:
   ```bash
   dotnet build Squirrel.Wiki.Plugins.TableOfContents.csproj
   ```

2. Copy the DLL to the Plugins directory:
   ```bash
   xcopy /Y bin\Debug\net8.0\*.dll ..\Squirrel.Wiki.Web\Plugins\Squirrel.Wiki.Plugins.TableOfContents\
   ```

3. Restart the application - the plugin will be automatically discovered

4. Navigate to `/plugins` in the admin interface

5. Enable the "Table of Contents" plugin

## Usage

### Basic Usage

Add the `{{toc}}` placeholder anywhere in your markdown content:

```markdown
# My Page Title

{{toc}}

## Introduction

This is the introduction section...

## Main Content

### Subsection 1

Content here...

### Subsection 2

More content...

## Conclusion

Final thoughts...
```

This will generate a table of contents with links to all headings from H1 up to the configured maximum depth.

### Configuration Options

The plugin supports one configuration option:

#### Maximum Heading Depth

- **Key**: `MaxDepth`
- **Type**: Number
- **Default**: `3`
- **Range**: 1-6
- **Description**: Maximum heading level to include in the TOC

Example: Setting to `2` will only include H1 and H2 headings.

**Note**: The plugin always includes all heading levels from H1 up to the configured MaxDepth.

### Generated HTML Structure

The plugin generates semantic HTML:

```html
<nav class="toc" role="navigation">
  <h2 class="toc-title">Table of Contents</h2>
  <ul class="toc-list">
    <li class="toc-item toc-level-1">
      <a href="#my-page-title" class="toc-link">My Page Title</a>
      <ul class="toc-list-nested">
        <li class="toc-item toc-level-2">
          <a href="#introduction" class="toc-link">Introduction</a>
        </li>
        <li class="toc-item toc-level-2">
          <a href="#main-content" class="toc-link">Main Content</a>
          <ul class="toc-list-nested">
            <li class="toc-item toc-level-3">
              <a href="#subsection-1" class="toc-link">Subsection 1</a>
            </li>
            <li class="toc-item toc-level-3">
              <a href="#subsection-2" class="toc-link">Subsection 2</a>
            </li>
          </ul>
        </li>
      </ul>
    </li>
  </ul>
</nav>
```

## How It Works

1. **Detection**: The plugin looks for `{{toc}}` in the rendered HTML
2. **Extraction**: It parses all heading tags (`<h1>` through `<h6>`) with their IDs
3. **Filtering**: Includes headings from H1 up to the configured MaxDepth
4. **Generation**: Creates a nested list structure based on heading hierarchy
5. **Replacement**: Replaces `{{toc}}` with the generated table of contents

## Requirements

- Squirrel Wiki v1.0+

## Troubleshooting

### TOC Not Appearing

- Ensure the plugin is enabled in `/plugins`
- Verify `{{toc}}` is spelled correctly (case-insensitive)
- Check that your page has headings with IDs (Markdig auto-generates these)

### Missing Headings

- Check the `MaxDepth` configuration setting
- Ensure headings are properly formatted in markdown
- Verify headings are within the configured depth range

## Version History

### 1.0.0 (2025-11-28)

- Initial release
- Basic TOC generation from headings
- Configurable maximum depth
- Nested list support for all heading levels
- Always includes H1 headings

## License

This plugin is part of Squirrel Wiki and follows the same license.