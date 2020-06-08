﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CourseToolHotReloader.Dtos;
using CourseToolHotReloader.Log;

namespace CourseToolHotReloader.ApiClient
{
	public class HttpMethods
	{
		private const string baseUrl = "http://localhost:8000";

		public static async Task<AccountTokenResponseDto> GetJwtToken(LoginPasswordParameters parameters)
		{
			var url = $"{baseUrl}/account/login";

			var json = JsonSerializer.Serialize(parameters);
			var data = new StringContent(json, Encoding.UTF8, "application/json");

			using var client = new HttpClient();

			var response = await HttpResponseMessage(client, url, data);

			if (response.StatusCode != HttpStatusCode.OK)
				throw new Exception("Неправильный логин или пароль");

			var result = response.Content.ReadAsStringAsync().Result;
			return JsonSerializer.Deserialize<AccountTokenResponseDto>(result);
		}

		private static async Task<HttpResponseMessage> HttpResponseMessage(HttpClient client, string url, HttpContent data)
		{
			HttpResponseMessage response;

			try
			{
				response = await client.PostAsync(url, data);
			}
			catch (HttpRequestException e)
			{
				ConsoleWorker.WriteError("Отсутствует соединение с сервером ulearn");
				throw;
			}

			return response;
		}

		public static async Task<TempCourseUpdateResponse> UploadCourse(MemoryStream memoryStream, string token, string id)
		{
			var url = $"{baseUrl}/tempCourses/uploadCourse/{id}";

			return await UpdateTempCourse(memoryStream, token, url);
		}

		public static async Task<TempCourseUpdateResponse> UploadFullCourse(MemoryStream memoryStream, string token, string id)
		{
			var url = $"{baseUrl}/tempCourses/uploadFullCourse/{id}";

			return await UpdateTempCourse(memoryStream, token, url);
		}

		public static async Task CreateCourse(string token, string id)
		{
			var url = $"{baseUrl}/tempCourses/create/{id}";

			using var client = HttpClient(token);

			var response = await HttpResponseMessage(client, url, null);

			BadCodeHandler(response);
		}

		private static async Task<TempCourseUpdateResponse> UpdateTempCourse(MemoryStream memoryStream, string token, string url)
		{
			using var client = HttpClient(token);

			var fileContent = new ByteArrayContent(memoryStream.ToArray());
			var multiContent = new MultipartFormDataContent { { fileContent, "files", "qwe.zip" } };
			var response = await HttpResponseMessage(client, url, multiContent);

			BadCodeHandler(response);

			var result = response.Content.ReadAsStringAsync().Result;
			return JsonSerializer.Deserialize<TempCourseUpdateResponse>(result);
		}

		private static void BadCodeHandler(HttpResponseMessage response)
		{
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
					return;
				case HttpStatusCode.Unauthorized:
					ConsoleWorker.WriteError("Срок авторизации истек, требуется повторная авторизация");
					Environment.Exit(1);
					return;
				case HttpStatusCode.InternalServerError:
					ConsoleWorker.WriteError($"На сервере произошла ошибка: {response.Content.ReadAsStringAsync().Result}");
					Environment.Exit(1);
					return;
			}
		}

		private static HttpClient HttpClient(string token)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", token);
			return client;
		}
	}
}