using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace QuizRT.Models{
    public class QuizRTRepo : IQuizRTRepo {
        int NumberOfQuestions = 100;
        int optionNumber = 3;
        // List<string> optionReviewList = new List<string>();
        QuizRTContext context = null;
        public QuizRTRepo(QuizRTContext _context){
            this.context = _context;
        }
        public List<QuizRTTemplate> GetTemplate(){
            return context.QuizRTTemplateT.ToList();
        }
        public List<Questions> GetQuestion(){
            return context.QuestionsT
            .Include( n => n.QuestionOptions )
            .ToList();
        }
        public List<Options> GetOption(){
            return context.OptionsT.ToList();
        }

        public List<Questions> GetQuestionOnlyWithoutInsertion(QuizRTTemplate template){
            List<Questions> g_question_notable = new List<Questions>();
            Task<string> id= Gettopic_id("https://www.wikidata.org/w/api.php?action=wbsearchentities&search="+template.TopicName+"&language=en&format=json");
            string f_id = id.Result;
            string sparQL = "SELECT ?personLabel WHERE { ?person wdt:P106 wd:"+f_id+" . SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 100";
            Task<List<string>> questions = GetQuestionData(sparQL);
            List<string> all_questions_without_tables = questions.Result;
            Task<List<string>> options= GetOptionData("SELECT ?cid ?options WHERE {?cid wdt:P31 wd:Q28640. OPTIONAL {?cid rdfs:label ?options filter (lang(?options) = 'en') . }}Limit 100");
            List<string> all_options = options.Result;
            for(int i=0;i<all_questions_without_tables.Count;i++)
            {
                Questions s_object = new Questions();
                s_object.QuestionGiven="What is "+all_questions_without_tables[i]+" Occupation ?";
                List<Options> mut_options_single_q = randomizeOptions(all_options,template.TopicName);
                s_object.QuestionOptions=mut_options_single_q;
                g_question_notable.Add(s_object);
            }
            return g_question_notable;
        }

       public List<Options> randomizeOptions(List<string> optionReviewList,string topic_name){
           List<string> other_options = new List<string>();
           List<Options> g_List_options_notable = new List<Options>();
           List<int> randomNumber = getRandonNumber(0, optionReviewList.Count-1, optionNumber-1);
           for(int i=0; i < randomNumber.Count ; )
           {
               Options g_options_notable_f = new Options();
               g_options_notable_f.OptionGiven=optionReviewList[randomNumber[i]];
               g_options_notable_f.IsCorrect=false;
               g_List_options_notable.Add(g_options_notable_f); 
               i++;
           }
           Options g_options_notable = new Options();
               g_options_notable.OptionGiven=topic_name;
               g_options_notable.IsCorrect=true;
               g_List_options_notable.Add(g_options_notable);  
           return g_List_options_notable;
       }

       async Task<string> Gettopic_id(string sparQL){
           //string baseUrl = "https://query.wikidata.org/sparql?query="+sparQL+"&format=json";
           //The 'using' will help to prevent memory leaks.
           //Create a new instance of HttpClient
           using (HttpClient client = new HttpClient())
           //Setting up the response...
           using (HttpResponseMessage res = await client.GetAsync(sparQL))
           using (HttpContent content = res.Content){
               string data = await content.ReadAsStringAsync();
               // JObject data = await content.ReadAsAsync<JObject>();
               JObject json = JObject.Parse(data);
               JArray J = (JArray)json["search"];
               string str = (string)J[0]["id"];
               return str;
           }
       }
        public bool PostQuery(QuizRTTemplate qT){    Console.WriteLine("---Inside-PostQuery---");
            // JObject jo = (JObject)(q);
            // string categId = jo["categ"].ToString();
            // string topicId = jo["topic"].ToString();
            // string categName = jo["categName"].ToString();
            // string topicName = jo["topicName"].ToString();
            if( context.QuizRTTemplateT.FirstOrDefault( n => n.Categ == qT.Categ) == null ){
                // QuizRTTemplate qT = new QuizRTTemplate();
                // qT.Categ = categId;
                // qT.CategName = categName;
                // qT.Topic = topicId;
                // qT.TopicName = topicName;
                context.QuizRTTemplateT.Add(qT);
                context.SaveChanges();  Console.WriteLine("---Template-Table-Inserted---");

                if( context.QuestionsT.FirstOrDefault( n => n.Categ == qT.Categ) == null ){
                    if(qT.TopicName == "Occupation"){   Console.WriteLine("---Inside-Occupation---");
                        if ( GenerateQuestion(qT) && GenerateOptions(qT) ) 
                            return true;
                    } else {
                        if( GenerateQuestion(qT) )
                            return true;
                    }
                }
                return true;
            }
            return false;
        }
        // public bool PostTemplate(Questions q){// QuizRTTemplate
        //     // if( context.QuizRTTemplateT.FirstOrDefault( n => n.Categ == q.Categ) == null ){
        //     //     context.QuizRTTemplateT.Add(q);
        //     //     context.SaveChanges();
        //     //     return true;
        //     // }
        //     // return false;
        //     context.QuestionsT.Add(q);
        //     context.SaveChanges();
        //     return true;
        // }
        public void DeleteTemplate(){
            List<QuizRTTemplate> Lqt = context.QuizRTTemplateT.ToList();
            if( Lqt.Count > 0 ){
                context.Database.ExecuteSqlCommand("TRUNCATE TABLE QuizRTTemplateT");
                // context.RemoveRange(Lqt);
                // context.SaveChanges();
            }
            List<Questions> LqtQ = context.QuestionsT.ToList();
            if( LqtQ.Count > 0 ){
                // context.Database.ExecuteSqlCommand("TRUNCATE TABLE [QuestionsT]");
                context.RemoveRange(LqtQ);
                context.SaveChanges();
            }
            List<Options> Lops = context.OptionsT.ToList();
            if( Lops.Count > 0 ){
                // context.Database.ExecuteSqlCommand("TRUNCATE TABLE [OptionsT]");
                context.RemoveRange(Lops);
                context.SaveChanges();
            }
        }

        // ------------------------

        public bool GenerateQuestion(QuizRTTemplate q) {    Console.WriteLine("---Inside-GenerateQuestion---");
            if(q.TopicName == "Occupation"){
                string sparQL = "SELECT ?personLabel WHERE { ?person wdt:"+q.Topic+" wd:"+q.Categ+" . SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . } }LIMIT "+NumberOfQuestions+"";
                // string sparQL2 = $@"SELECT ?personLabel WHERE {{ ?person wdt:{q.Topic} wd:{q.Categ} . SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . } }LIMIT "+NumberOfQuestions+""; // Nishant
                Task<List<string>> dataReturns = System.Threading.Tasks.Task<string>.Run(() => GetQuestionData(sparQL).Result);
                List<string> quesReviewList = dataReturns.Result;

                List<Questions> qL = new List<Questions>();
                for(int i=0; i<quesReviewList.Count; i++){
                    Questions ques = new Questions();
                    ques.QuestionGiven = "What is "+quesReviewList[i]+" "+q.TopicName+"?";
                    ques.Topic = q.TopicName;
                    ques.Categ = q.CategName;
                    qL.Add(ques);
                }
                context.QuestionsT.AddRange(qL);
                context.SaveChanges();
                return true;
            }
            else if( q.TopicName != "Occupation" ){
                string sparQL = "";
                //List<string> other_options = new List<string>();
                Task<string> id= Gettopic_id("https://www.wikidata.org/w/api.php?action=wbsearchentities&search="+q.TopicName+"&language=en&format=json");
                string f_id = id.Result;
                if(q.TopicName=="Book")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+f_id+".?cid wdt:P50 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName=="princely state of the British Raj"){
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:Q1336152 . ?cid wdt:P17 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";
                }
                else if(q.TopicName=="state of the United States")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P138 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName=="business")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P571 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName!="princely state of the British Raj")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P17 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName!="princely state of the British Raj")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P17 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName!="princely state of the British Raj")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P17 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName!="princely state of the British Raj")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P17 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                else if(q.TopicName!="princely state of the British Raj")
                    sparQL = "SELECT ?cidLabel ?authortitleLabel WHERE {?cid wdt:P31 wd:"+q.Topic+".?cid wdt:P17 ?authortitle .SERVICE wikibase:label { bd:serviceParam wikibase:language 'en' . }}Limit 10";

                Task<List<universal_object>> dataReturns = System.Threading.Tasks.Task<string>.Run(() => GetQuestionData_others(sparQL).Result);
                List<universal_object> quesReviewList = dataReturns.Result;
                // Console.WriteLine(quesReviewList.Count);
                List<Questions> qL = new List<Questions>();
                
                List<string> books_etc_options = GenerateOptions1(q);
                
                for(int i=0; i<quesReviewList.Count; i++){
                    Questions ques = new Questions();
                    if(q.TopicName=="Book")
                        ques.QuestionGiven = "Who is the author of "+quesReviewList[i].mainobject+"?";
                    else if(q.TopicName=="princely state of the British Raj"){
                        ques.QuestionGiven = " "+quesReviewList[i].mainobject+" belongs to which country ?";
                    }
                    else if(q.TopicName=="state of the United States")
                        ques.QuestionGiven = " "+quesReviewList[i].mainobject+" is named after ?";
                    else if(q.TopicName=="business"){
                        ques.QuestionGiven = "When was "+quesReviewList[i].mainobject+" established ?";
                    }
                    ques.Topic = q.TopicName;
                    ques.Categ = q.CategName;
                    //qL.Add(ques);
                    context.QuestionsT.Add(ques);
                    Options op = new Options();
                    op.IsCorrect=true;
                    op.QuestionsId=ques.QuestionsId;
                    op.OptionGiven=quesReviewList[i].predicate; 
                    context.OptionsT.Add(op);
                    List<int> randomNumber = getRandonNumber(0, books_etc_options.Count-1, optionNumber+2);
                    for(int j=0;j<3;j++){
                        Options op1 = new Options();
                        op1.IsCorrect=false;
                        op1.QuestionsId=ques.QuestionsId;
                        // if(books_etc_options[randomNumber[k]]!=op.OptionGiven)
                        // {
                        op1.OptionGiven = books_etc_options[randomNumber[j]];
                        // j++;
                        context.OptionsT.Add(op1);
                        //}
                        // k++;
                    }  
                }
                //context.QuestionsT.AddRange(qL);
                context.SaveChanges();
                return true;
            }
            return false;
        }
        public List<string> GenerateOptions1(QuizRTTemplate q) { // For generating options other than Occupation
            string sparQL = "";
            if(q.TopicName=="Book")
                sparQL = "SELECT ?cid ?options WHERE {?cid wdt:P106 wd:Q482980. OPTIONAL {?cid rdfs:label ?options filter (lang(?options) = 'en') . }}Limit "+NumberOfQuestions*10+"";
            else if(q.TopicName=="princely state of the British Raj")
                sparQL = "SELECT ?cid ?options WHERE {?cid wdt:P31 wd:Q6256. OPTIONAL {?cid rdfs:label ?options filter (lang(?options) = 'en') . }}Limit "+NumberOfQuestions*10+"";
            else if(q.TopicName=="state of the United States")  
                sparQL = "SELECT ?cid ?options WHERE {?cid wdt:P166/wdt:P31 wd:Q7191. OPTIONAL {?cid rdfs:label ?options filter (lang(?options) = 'en') . }}Limit "+NumberOfQuestions*10+"";
            else if(q.TopicName=="business")  
                sparQL = "SELECT DISTINCT ?person ?personLabel ?options WHERE {?person wdt:P31 wd:Q3918.?person wdt:P571 ?options SERVICE wikibase:label {bd:serviceParam wikibase:language '[AUTO_LANGUAGE],en' .}}Limit "+NumberOfQuestions*10+"";
            Task<List<string>> dataReturns = System.Threading.Tasks.Task<string>.Run(() => GetOptionData(sparQL).Result);
            List<string> optionReviewList = dataReturns.Result;
            return optionReviewList;
        }
        async Task<List<universal_object>> GetQuestionData_others(string sparQL){ // Generating question related to some object other than occupation
            List<universal_object> universal_list_objects = new List<universal_object>();
            string baseUrl = "https://query.wikidata.org/sparql?query="+sparQL+"&format=json";
            //The 'using' will help to prevent memory leaks.
            //Create a new instance of HttpClient
            using (HttpClient client = new HttpClient())
            //Setting up the response...         
            using (HttpResponseMessage res = await client.GetAsync(baseUrl))
            using (HttpContent content = res.Content){
                string data = await content.ReadAsStringAsync();
                // JObject data = await content.ReadAsAsync<JObject>();
                //Console.WriteLine(data);
                JObject json = JObject.Parse(data);
                JArray j  = ((JArray)json["results"]["bindings"]);
                if (data != null){
                    // var GeneratedSubject = ((JArray)json["results"]["bindings"]).Select(s => s["cidLabel"]["value"].ToString());
                    // var GeneratedPredicate = ((JArray)json["results"]["bindings"]).Select(s => s["authortitleLabel"]["value"].ToString());
                    for(int i=0; i < j.Count ; i++){ 
                        universal_object s_universe = new universal_object(); 
                        //Console.WriteLine("---"+(j[i]["cidLabel"]["value"].ToString()));
                        s_universe.mainobject = (j[i]["cidLabel"]["value"].ToString());
                        //Console.WriteLine("---"+(j[i]["authortitleLabel"]["value"]));
                        s_universe.predicate = (string)j[i]["authortitleLabel"]["value"];
                        universal_list_objects.Add(s_universe);
                    }
                    
                    return universal_list_objects;
                }
                return new List<universal_object>();
            }
        }
        async Task<List<string>> GetQuestionData(string sparQL){    Console.WriteLine("---Inside-GetQuestionData---");
            List<string> quesReviewList = new List<string>();
            string baseUrl = "https://query.wikidata.org/sparql?query="+sparQL+"&format=json";
            //The 'using' will help to prevent memory leaks.
            //Create a new instance of HttpClient
            using (HttpClient client = new HttpClient())
            //Setting up the response...         
            using (HttpResponseMessage res = await client.GetAsync(baseUrl))
            using (HttpContent content = res.Content){
                string data = await content.ReadAsStringAsync();
                // JObject data = await content.ReadAsAsync<JObject>();
                JObject json = JObject.Parse(data);
                if (data != null){
                    var GeneratedQuestions = ((JArray)json["results"]["bindings"]).Select(s => s["personLabel"]["value"].ToString());    // Nishant
                    // for(int i=0; i < ((JArray)json["results"]["bindings"]).Count ; i++){  
                    //     quesReviewList.Add(json["results"]["bindings"][i]["personLabel"]["value"].ToString());
                    // }
                    quesReviewList = GeneratedQuestions.ToList<string>();
                }
                return quesReviewList;  // Nishant
            }
        }
        public bool GenerateOptions(QuizRTTemplate q) { Console.WriteLine("---Inside-GenerateOptions---");
            string sparQL = "SELECT ?cid ?options WHERE {?cid wdt:P31 wd:Q28640. OPTIONAL {?cid rdfs:label ?options filter (lang(?options) = 'en') . }}Limit "+NumberOfQuestions*10+"";
            Task<List<string>> dataReturns = System.Threading.Tasks.Task<string>.Run(() => GetOptionData(sparQL).Result);
            List<string> optionReviewList = dataReturns.Result;
            
            List<Questions> qL = context.QuestionsT
                                        .Where( n => n.Categ == q.CategName)
                                        .ToList();
            
            for(int i=0; i<qL.Count; i++){
                List<Options> oL = new List<Options>();
                oL = randomizeOptions(optionReviewList, q.CategName, qL[i].QuestionsId);
                context.OptionsT.AddRange(oL);
            }
            context.SaveChanges();
            return true;
        }
        async Task<List<string>> GetOptionData(string sparQL){ Console.WriteLine("---Inside-GetOptionData---");
            List<string> optionReviewList = new List<string>();
            string baseUrl = "https://query.wikidata.org/sparql?query="+sparQL+"&format=json";
            //The 'using' will help to prevent memory leaks.
            //Create a new instance of HttpClient
            using (HttpClient client = new HttpClient())
            //Setting up the response...         
            using (HttpResponseMessage res = await client.GetAsync(baseUrl))
            using (HttpContent content = res.Content){
                string data = await content.ReadAsStringAsync();
                // JObject data = await content.ReadAsAsync<JObject>();
                JObject json = JObject.Parse(data);
                // JArray J = (JArray)json["results"]["bindings"];
                if (data != null){
                    // var GeneratedOptions = ((JArray)json["results"]["bindings"]).Select(s => s["options"]["value"].ToString());
                    for(int i=0; i < ((JArray)json["results"]["bindings"]).Count ; i++){
                        if ( ((JArray)json["results"]["bindings"])[i].Count() >= 2)
                            optionReviewList.Add(json["results"]["bindings"][i]["options"]["value"].ToString());
                    }
                    return optionReviewList;
                }
                return new List<string>();
            }
        }
        public List<Options> randomizeOptions(List<string> optionReviewList, string categName, int qId){
            List<int> randomNumber = getRandonNumber(0, optionReviewList.Count-1, optionNumber-1);

            List<Options> optionPerQues = new List<Options>();
            for(int i=0; i < randomNumber.Count ; i++) {
                // if(optionReviewList[i] == categName){
                    Options ops = new Options();
                    ops.OptionGiven = optionReviewList[randomNumber[i]];
                    ops.IsCorrect = false;
                    ops.QuestionsId = qId;
                    optionPerQues.Add(ops);
                // } else {
                //     randomizeOptions(optionReviewList, categName, qId);
                // }
            }
            Options opsCorrect = new Options();
            opsCorrect.OptionGiven = categName;
            opsCorrect.IsCorrect = true;
            opsCorrect.QuestionsId = qId;
            optionPerQues.Add(opsCorrect);
            // shuffling the option to create randomness
            // optionPerQues = shuffle(optionPerQues);
            return optionPerQues;
        }
        public List<int> getRandonNumber(int iFromNum, int iToNum, int iNumOfItem){
            List<int> lstNumbers = new List<int>();
            Random rndNumber = new Random();

            int number = rndNumber.Next(iFromNum, iToNum + 1);
            lstNumbers.Add(number);
            int count = 0;
            do{
                number = rndNumber.Next(iFromNum, iToNum + 1);
                if (!lstNumbers.Contains(number)){
                    lstNumbers.Add(number);
                    count++;
                }
            } while (count < iNumOfItem);
            return lstNumbers;
        }
        public List<Options> shuffle(List<Options> optionPerQues){

            return optionPerQues;
        }
    }

}