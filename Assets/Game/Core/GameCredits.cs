using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public static class GameCredits
    {
        public const string ChundongiMemorialTitle = "춘동이를 기억하며";
        public const string ChundongiMemorialBody =
            "2019년 저의 품에 들어온 춘동이는\n" +
            "2025년 12월 15일 무지개다리를 건넜습니다.\n\n" +
            "더 많은 것을 해주지 못한 아쉬움이 가득하지만,\n" +
            "이 게임 안에서, 그리고 하늘에서도\n" +
            "언제나 사랑받으며 행복하게 지내길 바랍니다.\n\n" +
            "나와 함께해줘서 고마워. 사랑해, 춘동아.";

        public static readonly IReadOnlyList<string> NoticeTitles = Array.AsReadOnly(new[]
        {
            "상태 아이콘", "효과음 출처", "추가 자산", "Galmuri", "Liberation Sans", "소프트웨어 고지"
        });

        public static readonly IReadOnlyList<string> NoticeFiles = Array.AsReadOnly(new[]
        {
            "GameIcons.txt", "AudioSources.txt", "OtherAssets.txt", "Galmuri-OFL.txt", "LiberationSans-OFL.txt", "Software.txt"
        });

        public static string ReadNotice(int index)
        {
            // Windows release: StreamingAssets also exposes the originals beside the installed game.
            string path = Path.Combine(Application.streamingAssetsPath, "Notices", NoticeFiles[index]);
            try { return File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Credits] Cannot read " + NoticeFiles[index] + ": " + e.Message);
                return "고지 파일을 불러오지 못했습니다. 게임 파일 무결성을 확인해 주세요.";
            }
        }
    }
}
