# Swagger toolkit beta

Available (free) in the [Microsoft Store](https://www.microsoft.com/store/apps/9n5vkgq9dvg3).

## Summary

An open source toolkit for editing JSON swagger files.

## Product features

- Supports editing the API path summary/description fields using Markdown.
- Converts multiline Markdown to singleline Markdown, which is required for insertion into a JSON swagger file.
- Applies a consistent JSON document format.
- Uses the Microsoft [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview) library.
- Optionally, extract the summary/description in JSON format without saving to a swagger file.
- Supports quick insert of [Docs Markdown](https://marketplace.visualstudio.com/items?itemName=docsmsft.docs-markdown) constructs (e.g. Notes, Tips).

## Upcoming features

- Configurable character escaping.
- Support for editing more swagger fields.
- Autorest validation.

## Getting started

1. To get started, drag and drop a JSON swagger file onto the path textbox. You can test the app using this [sample](./resources/samples/sample-swagger.json) swagger file.

    ![Screenshot showing the swagger file drop area.](./resources/screenshots/screenshot-1.png)

2. Choose from the **Select an API tag** dropdown list. The dropdown contains all [tags](https://swagger.io/docs/specification/grouping-operations-with-tags/) from the swagger file.

3. Choose from the **Select an API operation** dropdown list. The dropdown contains all [operationIds](https://swagger.io/docs/specification/paths-and-operations/) from the swagger file that are relevant to the selected [tag](https://swagger.io/docs/specification/grouping-operations-with-tags/).

4. Edit the summary and/or description. You can use the snippet buttons (e.g. **Note**) to add a [Docs Markdown](https://marketplace.visualstudio.com/items?itemName=docsmsft.docs-markdown) construct at the current cursor position (only applies to the description textbox). 

    ![Screenshot showing the summary and description text entry for a REST API path.](./resources/screenshots/screenshot-2.png)

5. Optionally, select one of the **Get JSON** buttons to convert summary/description content to JSON, and save it in your system clipboard. This option doesn't save changes to the swagger file.

6. Choose **Undo changes** or **Save to swagger** to discard or save your edits for the current API page. If you navigate to a new API page without first selecting **Save to swagger**, the app will discard any edits.

Verify your changes in a diff viewer.

## Manually compile a single file executable

Although it's easier to install the app from the Microsoft Store, you can compile and install the app using Visual Studio.

### Prerequisites

- [.NET 5+](https://dotnet.microsoft.com/download)
- [Visual Studio 2019+](https://visualstudio.microsoft.com/vs/)

### Create an executable

1. Open the project in Visual Studio.

1. From the menu bar, choose **Build** > **Publish...**.

1. Choose to publish to a folder, then select **Finish**.

1. Choose **Show all settings**.

1. Choose **Framework-dependent** and **Produce single file** in the profile settings, and note the **Target location** of the compiled executable.

1. Choose **Publish**.
