using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace FreeDraw
{
    public class questionSample
    {
        public string question;
        public bool used;

        public questionSample(string q, bool u)
        {
            this.question = q;
            this.used = u;
        }
    }
    public class GM : MonoBehaviour
    {
        public enum Round
        {
            non,
            player1,
            player2
        }

        public enum Process
        {
            start,
            getName,
            decidePlayer,
            end,
        }

        public Round round = Round.non;
        public Process process = Process.start;
        int roundCount = 0;

        private float countDown = 3.0f;
        
        private string question = "";
        private string prompt;
        private string sysChat = "";
        private int rightAnsCount = 0;
        private string sysScore = "";
        private int nameCount = 0;
        private float drawTime = 120.0f;
        private float roundCountDown;
        private bool promptState1 = false;

        public List<Drawable> allPlayer = new List<Drawable>();
        public List<questionSample> allQuestion = new List<questionSample>();

        public void Login(Drawable player)
        {
            allPlayer.Add(player);
            player.RpcSetPlayer(allPlayer.Count);
            
        }

        public void AddRound()
        {
            roundCount++;
        }

        public void GetName(string name, int id)
        {
            allPlayer[id].name = name;
            nameCount++;
        }

        public void UseShadingItem(int id)
        {
            allPlayer[id].RpcOpenShading();
            Debug.Log("ServerUse");
        }

        public void CheckAns(string ans, int id)
        {
            if (ans == question)
            { 
                rightAnsCount++;
                allPlayer[id].score += (int)(roundCountDown * 20);
                sysChat += "\n" + allPlayer[id].name + "答對了!!";
                RefreshChatText();
                if (rightAnsCount == (allPlayer.Count - 1))
                {
                    MakeQuestion();
                    GivePrompt();
                    rightAnsCount = 0;
                    roundCountDown = drawTime;
                    roundCount++;
                }
                GetScore();
            }
            else
            {
                sysChat += "\n" + allPlayer[id].name + ": " + ans;
                RefreshChatText();
            }
        }

        void RefreshChatText()
        {
            foreach (Drawable pl in allPlayer)
            {
                pl.sysChat = sysChat;
            }
        }

        void GetScore()
        {
            sysScore = "";
            foreach (Drawable pl in allPlayer)
            {
                sysScore += pl.name + ":\t" + pl.score + "\n";
            }
            foreach (Drawable pl in allPlayer)
            {
                pl.sysScore = sysScore;
            }
        }

        void Start()
        {
            string filePath = Application.dataPath + "/StreamingAssets";
            string nameAndPath = filePath + "/" + "Question.txt";//存檔的位置加檔名

            //StreamReader _streamReader = File.OpenText(nameAndPath);
            StreamReader _streamReader = new System.IO.StreamReader(nameAndPath, System.Text.Encoding.Default);
            while (!_streamReader.EndOfStream)
            {
                string data = _streamReader.ReadLine();//讀取所有存檔
                questionSample q = new questionSample(data, false);
                allQuestion.Add(q);
            }
            _streamReader.Close();//記得要關閉，不然會報錯            
            MakeQuestion();
        }

        void GivePrompt()
        {
            foreach (Drawable pl in allPlayer)
            {
                pl.sysAns = prompt;
            }
        }

        void MakeQuestion()
        {
            //出題

            int num = Random.Range(0, allQuestion.Count);
            while(allQuestion[num].used == true)
            {
                num = Random.Range(0, allQuestion.Count);
            }
            question = allQuestion[num].question;
            allQuestion[num].used = true;
            //給提示
            prompt = "_";
            for (int i = 1; i < question.Length; i++)
            {
                prompt += " _";
            }
            //刷新階段
            promptState1 = false;
        }

        void MakePrompt()
        {
            int tmp = Random.Range(0, question.Length);
            if (tmp != 0)
                prompt = "_";
            else
                prompt = question[tmp] + "";
            for (int i = 1; i < question.Length; i++)
            {
                if(i!=tmp)
                    prompt += " _";
                else
                    prompt += " " + question[tmp];
            }
        }

        void Update()
        {
            switch (process)
            { 
                
                case Process.start:
                    
                    if (countDown <= 0)
                    {
                        foreach (Drawable pl in allPlayer)
                        {
                            pl.RpcSetUI();
                        }
                        process = Process.getName;
                    }
                    else
                    {
                        //分配id
                        int idNum = 0;
                        foreach (Drawable pl in allPlayer)
                        {
                            pl.id = idNum;
                            pl.playersNum = allPlayer.Count;
                            idNum++;
                        }
                        //sysMsg
                        foreach (Drawable pl in allPlayer)
                        {
                            pl.sysMsg = "等待玩家連線..." + Mathf.Ceil(countDown).ToString();
                        }
                        countDown -= Time.deltaTime;
                        
                    }
                    
                    
                    break;

                case Process.getName:
                    foreach (Drawable pl in allPlayer)
                    {
                        pl.sysMsg = "取名子吧";
                        pl.SetProcess(Drawable.Process.creatName);
                    }
                    if (nameCount == allPlayer.Count)
                    {
                        //給予其他玩家資訊
                        foreach (Drawable pl in allPlayer)
                        {
                            foreach (Drawable plInfo in allPlayer)
                            {
                                pl.RpcGetPlayerInfo(plInfo.name, plInfo.id);   
                            }
                        }
                        process = Process.decidePlayer;
                        GetScore();
                        GivePrompt();
                        roundCountDown = drawTime;
                    }
                    break;

                case Process.decidePlayer:
                    if(allPlayer.Count != 0)
                        roundCount = roundCount % allPlayer.Count;
                    roundCountDown -= Time.deltaTime;
                    if (roundCountDown < 0)
                    {
                        MakeQuestion();
                        GivePrompt();
                        roundCountDown = drawTime;
                        roundCount++;
                    }
                    if (roundCountDown <= 30 && promptState1 == false)
                    {
                        promptState1 = true;
                        MakePrompt();
                        GivePrompt();
                    }
                    for (int a = 0; a < allPlayer.Count; a++)
                    {
                        if (a == roundCount)
                        {

                            allPlayer[a].sysMsg = "你是畫家\n題目: " + question + "\n剩餘時間: " + (int)roundCountDown;
                            allPlayer[a].SetProcess(Drawable.Process.action);
                        }
                        else
                        {
                            allPlayer[a].sysMsg = "你要猜題" + "\n剩餘時間: " + (int)roundCountDown;
                            allPlayer[a].SetProcess(Drawable.Process.wait);
                        }
                    }
                    break;

            }
        }
    }
}