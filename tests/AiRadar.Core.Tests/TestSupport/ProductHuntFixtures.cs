namespace AiRadar.Core.Tests.TestSupport;

/// <summary>Responses in the shape of the Product Hunt API v2 GraphQL "posts" query.</summary>
internal static class ProductHuntFixtures
{
    public const string FirstPage = """
        {
          "data": {
            "posts": {
              "edges": [
                {
                  "node": {
                    "id": "1001",
                    "name": "Sketchloom",
                    "tagline": "Turn rough sketches into UI mockups",
                    "description": "Draw on paper, snap a photo, get a Figma-ready layout.",
                    "url": "https://www.producthunt.com/posts/sketchloom",
                    "votesCount": 412,
                    "createdAt": "2026-10-07T07:01:00Z"
                  }
                },
                {
                  "node": {
                    "id": "1002",
                    "name": "Mosaic Chat",
                    "tagline": "One chat window that remembers your projects",
                    "description": null,
                    "url": "https://www.producthunt.com/posts/mosaic-chat",
                    "votesCount": 540,
                    "createdAt": "2026-10-06T22:15:30+05:30"
                  }
                }
              ],
              "pageInfo": { "hasNextPage": true, "endCursor": "cursor-page-2" }
            }
          }
        }
        """;

    public const string SecondPage = """
        {
          "data": {
            "posts": {
              "edges": [
                {
                  "node": {
                    "id": "1003",
                    "name": "VoiceNote AI",
                    "tagline": "Records calls and writes searchable summaries",
                    "description": "Summaries in 20 languages.",
                    "url": "https://www.producthunt.com/posts/voicenote-ai",
                    "votesCount": 98,
                    "createdAt": "2026-10-05T10:00:00Z"
                  }
                }
              ],
              "pageInfo": { "hasNextPage": false, "endCursor": "cursor-page-3" }
            }
          }
        }
        """;

    public const string Empty = """
        { "data": { "posts": { "edges": [], "pageInfo": { "hasNextPage": false, "endCursor": null } } } }
        """;

    public const string GraphQlError = """
        { "errors": [ { "message": "Argument 'topic' on Field 'posts' has an invalid value." } ], "data": null }
        """;
}
