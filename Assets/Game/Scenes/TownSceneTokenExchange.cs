using System;
using BombBrick.Game.Core;
using UnityEngine;
using GUI = LocalizedGUI;

namespace BombBrick.Game.Scenes
{
    public sealed partial class TownSceneController
    {
        private string exchangeMonsterId;
        private string exchangeError;

        private static string ExchangeText(string key,string fallback) =>
            GameLocalization.TextByKey("town.exchange."+key,fallback);

        public bool TryCloseMonsterExchange()
        {
            if(string.IsNullOrEmpty(exchangeMonsterId))return false;
            exchangeMonsterId=null;exchangeError=null;
            return true;
        }

        private void DrawMonsterExchangeButton(Rect rect,OwnedMonsterSave monster)
        {
            if(profile.expeditionPreset.rosterInstanceIds.Contains(monster.instanceId))return;
            string block=PlayerProfileStore.MonsterExchangeBlock(profile,monster,activeRun);
            string label=string.Format(ExchangeText("button","증표 {0}개로 교환"),PlayerProfileStore.MonsterTokenValue(monster));
            if(BombBrickUiTheme.Button(rect,label,block==null,false,14))
            {
                exchangeMonsterId=monster.instanceId;exchangeError=null;
                PrepareForSystemMenu();
            }
            if(block!=null&&rect.Contains(Event.current.mousePosition))
                BombBrickUiTheme.DrawHoverTooltip(Event.current.mousePosition,
                    ExchangeText("title","동료 증표 교환"),ExchangeText("unavailable","교환할 수 없습니다"),
                    ExchangeText(block,block=="protected"?"잠금·즐겨찾기 또는 추모 동료는 교환할 수 없습니다.":
                        block=="minimum"?"탐험을 위해 동료 8마리는 남겨야 합니다.":
                        block=="roster"?"탐험에 편성된 동료는 교환할 수 없습니다.":"동료를 찾을 수 없습니다."),
                    BombBrickUiTheme.Orange);
        }

        private void DrawMonsterExchange()
        {
            OwnedMonsterSave monster=FindOwned(exchangeMonsterId);
            if(monster==null){TryCloseMonsterExchange();return;}
            BombBrickUiTheme.DrawModalBackdrop();
            Rect panel=new Rect((BombBrickUiLayout.Width-650f)*.5f,(BombBrickUiLayout.Height-420f)*.5f,650f,420f);
            BombBrickUiTheme.DrawPanel(panel,true);
            GUI.Label(new Rect(panel.x+24f,panel.y+20f,602f,38f),ExchangeText("title","동료 증표 교환"),
                BombBrickUiTheme.TitleStyle(26,TextAnchor.MiddleCenter));
            DrawMonsterPortrait(new Rect(panel.x+36f,panel.y+78f,112f,112f),monster);
            GUI.Label(new Rect(panel.x+170f,panel.y+76f,444f,32f),MonsterName(monster),BombBrickUiTheme.TitleStyle(23));
            GUI.Label(new Rect(panel.x+170f,panel.y+112f,444f,24f),GameLocalization.Text("평균 ")+MonsterStatQuality.OverallGrade(monster),BombBrickUiTheme.BodyStyle(17));
            DrawSpeciesProgression(new Rect(panel.x+170f,panel.y+145f,444f,66f),database.Get(monster.monsterId),monster);
            GUI.Label(new Rect(panel.x+24f,panel.y+223f,602f,28f),ExchangeText("notice","교환하면 이 동료는 보유 목록에서 제외됩니다."),
                BombBrickUiTheme.BodyStyle(16,TextAnchor.MiddleCenter));
            GUI.Label(new Rect(panel.x+24f,panel.y+260f,602f,34f),
                string.Format(ExchangeText("reward","받을 증표: {0}개"),PlayerProfileStore.MonsterTokenValue(monster)),
                BombBrickUiTheme.TitleStyle(22,TextAnchor.MiddleCenter));
            if(!string.IsNullOrEmpty(exchangeError))
                GUI.Label(new Rect(panel.x+24f,panel.y+300f,602f,28f),exchangeError,BombBrickUiTheme.BodyStyle(14,TextAnchor.MiddleCenter));
            if(BombBrickUiTheme.Button(new Rect(panel.x+24f,panel.yMax-66f,290f,42f),GameLocalization.Text("취소")))
                TryCloseMonsterExchange();
            if(BombBrickUiTheme.Button(new Rect(panel.x+336f,panel.yMax-66f,290f,42f),ExchangeText("confirm","교환 확정"),
                PlayerProfileStore.MonsterExchangeBlock(profile,monster,activeRun)==null,true,18))
            {
                try
                {
                    if(!profileStore.ExchangeMonsterForTokens(profile,database,monster.instanceId,activeRun))return;
                    tooltipMonsterInstanceId="";TryCloseMonsterExchange();BuildEncyclopediaCache();
                    GameAudio.Play(GameAudio.Cue.Click);
                }
                catch(Exception exception)
                {
                    Debug.LogException(exception);
                    exchangeError=ExchangeText("save_failed","저장하지 못했습니다. 동료와 증표는 변경되지 않았습니다.");
                }
            }
        }
    }
}
