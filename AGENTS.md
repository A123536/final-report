# Repository agent instructions

- When a task requires internet search and a Tavily MCP tool is available, use `tavily_search` as the default search provider. Do not use built-in web search unless Tavily cannot perform the required operation or the user explicitly requests another provider.
- During extended work, monitor the remaining token budget. If the remaining budget reaches 5% or less, stop starting new work immediately, save all current repository and artifact changes, verify the saved state where practical, and report both the completed work and any unfinished items so that nothing is lost.
