using acme_publishing_api;
using acme_publishing_data;
using Newtonsoft.Json;
using NuGet.Protocol;

var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
var acme_data_url = config["acme-publishing-data-url"];
var distroAPIS = config.GetSection("DistributorAPIs").Get<List<DistributorAPI>>();
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("/triggerAPI", async (Microsoft.AspNetCore.Http.HttpContext context) =>
{
    string subscriptionId = context.Request.Query["subscriptionId"];
    List<CustomerSubscription> cussubs = new List<CustomerSubscription> { };
    List<DistributorAPI> apisToSend = new List<DistributorAPI>();
    try
    {
        using (HttpClient client = new HttpClient() { BaseAddress = new Uri(acme_data_url) })
        {
            // using subId
            // get all customers that has subbed and their country
            var dbURL = "CustomerSubscription/Subscription/" + subscriptionId;
            var httpResponse = await client.GetAsync(dbURL);
            var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
            var responseList = JsonConvert.DeserializeObject<CustomerSubscription[]>(jsonResponse);

            cussubs.AddRange(responseList);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }

    // in the config, get all distrib api that is within this countryn
    // trigger all their api to get the order
    List<string> countries = [.. cussubs.Select(x => x.CountryId).Distinct()];
    apisToSend = distroAPIS.Where(x => countries.Contains(x.countryId)).ToList();

    try
    {
        foreach (var item in apisToSend)
        {
            using (HttpClient client = new HttpClient() { BaseAddress = new Uri(item.apiURL) })
            {
                await client.PostAsync("/Orders?" + subscriptionId, new StringContent(""));
            }

            // after all triggering is done
            // send sub history as triggered in acme db

            using (HttpClient client = new HttpClient() { BaseAddress = new Uri(acme_data_url) })
            {
                var url = "SubscriptionsHistory?" +
                    $"SubscriptionId={subscriptionId}&" +
                    $"DistributorId={item.distributorId}&" +
                    $"CountryId={item.countryId}";
                var response = await client.PostAsync(url, null);

                Console.WriteLine(response.StatusCode);
            }

        }
    }
    catch (Exception e)
    {

    }

    return apisToSend;
});

app.Run();
