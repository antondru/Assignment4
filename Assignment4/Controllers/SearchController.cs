﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Assignment4.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Assignment4.Controllers
{
    // Hanterar API-calls till TimeEdit 
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ApiController] 
    public class SearchController : Controller
    {
        private string objectId;
        private string dateString;

        /// <summary>
        /// "Bygger sök-url efter kurskod och kör metoden DateStringBuilder(), returnerar statuskod samt lista på schemainbokningar genom GetListOfSchedules(). 
        ///  Är listan tom returneras statuskod 500."
        /// </summary>
        /// <param name="courseCode"> Sökt kurskod</param>
        /// <param name="startDate">Startdatum för sökningen</param>
        /// <param name="endDate"> Slutdatum för sökningen</param>
        /// <returns>IActionResult - Statuskod Ok och lista med aktuella bokningar eller Statuskod 500.</returns>
        [Route("{courseCode}/{startDate}/{endDate}")]
        [HttpGet]
        public IActionResult GetSearchCourse(string courseCode, string startDate, string endDate)
        {
            string searchUrl = $"https://cloud.timeedit.net/ltu/web/schedule1/objects.txt?max=15&fr=t&partajax=t&im=f&sid=3&l=sv_SE&search_text={courseCode}&types=28";

            SetDateString(startDate, endDate);
            var getList = GetListOfSchedules(searchUrl);

            if (getList.Count == 0)
            {
                return StatusCode(500);
            }   
            else
            {
                return Ok(getList);
            }
        }

        /// <summary>
        /// "Bygger datumsträng (dateString) för att skjuta in i URL."
        /// </summary>
        /// <param name="startDate">Startdatum för sökningen</param>
        /// <param name="endDate"> Slutdatum för sökningen</param>
        private void SetDateString(string startDate, string endDate)
        {
            startDate = startDate.Replace("-", string.Empty);
            startDate += ".x";

            endDate = endDate.Replace("-", string.Empty);
            endDate += ".x";

            dateString = startDate + "," + endDate;
        }

        /// <summary>
        /// "Tar ut alla objectids för sökt kurs från JSON-data från TimeEdit. 
        /// Returnerar lista med scheman som har bokningar för aktuell tidpunkt.
        /// Har man sökt på felaktig kurskod kommer metoden returnera en tom lista."
        /// </summary>
        /// <param name="searchUrl">URL med sökt kurskod</param>
        /// <returns>Lista med Root-objekt</returns>
        private List<Root> GetListOfSchedules(string searchUrl)
        {
            var jsonData = new WebClient().DownloadString(searchUrl);
            var userObj = JObject.Parse(jsonData);

            List<Root> scheduleList = new List<Root>();

            if (((JObject)userObj).Count != 0)
            {
                objectId = userObj.SelectToken("ids").ToString(); 
                string[] objectIdArray = objectId.Split(',');

                for (int i = 0; i < objectIdArray.Length; i++)
                {
                    objectIdArray[i] = objectIdArray[i]
                        .Replace("[", string.Empty)
                        .Replace("]", string.Empty)
                        .Replace("\r\n", string.Empty)
                        .Replace(" ", string.Empty);
                               
                    var root = GetScheduleByObjectId(objectIdArray[i]);

                    // kollar om root-objektets properties är null
                    bool isNull = root.GetType().GetProperties()
                                  .All(p => p.GetValue(root) == null);

                    // körs om root-objektets properties ej är null, saknas aktuella bokningar läggs inte det schemat till i scheduleList.
                    if (!isNull) 
                    {
                    scheduleList.Add(root);
                    }
                    
                }              
            }

            return scheduleList;           
        }

        /// <summary>
        /// "Hämtar jsondata för kursinfo, mappar det sedan till Models.CourseInfo"
        /// </summary>
        /// <param name="courseId">Kursens objekt-ID</param>
        /// <returns>CourseInfo-objekt</returns>
        private CourseInfo GetCourseInformation(string courseId)
        {
            var url = $"https://cloud.timeedit.net/ltu/web/schedule1/objects/{courseId}/o.json?fr=t&types=28&sid=3&l=sv_SE";
            var courseJson = new WebClient().DownloadString(url);

            return JsonConvert.DeserializeObject<CourseInfo>(courseJson);
        }

        /// <summary>
        /// "Hämtar JSON-objekt för aktuellt schema via kursens objekt-id och mappar det till ett Root-objekt"
        /// </summary>
        /// <param name="courseId">Kursens objekt-ID</param>
        /// <returns>Root-objekt (ett schema)</returns>
        private Root GetScheduleByObjectId(string courseId)
        {     
            var url = $"https://cloud.timeedit.net/ltu/web/schedule1/ri.json?h=t&sid=3&p={dateString}&objects={courseId}.28&ox=0&types=0&fe=0";
            string jsonSched;

            try
            {
                jsonSched = new WebClient().DownloadString(url);
            }
            catch (WebException e)
            {
                Console.WriteLine("Fel: {0}", e);
                return new Root();
            }    
            // mappar schema till Root-objektet (columnheaders, reservations, info)
            Root scheduleCollection = JsonConvert.DeserializeObject<Root>(jsonSched);

            // kör metoden GetCourseInformation (mappar kursinformation till courseinfo i Root-objektet)
            scheduleCollection.courseinfo = GetCourseInformation(courseId);

            return scheduleCollection;    
        }
    }
}

