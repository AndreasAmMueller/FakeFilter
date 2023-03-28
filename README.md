# FakeFilter API

FakeFilter aims to monitor fake/temp e-mail providers, so site operators can distinguish whether the e-mail address is a serious client.

This is an implementation of the RESTful API of [FakeFilter.net](https://fakefilter.net/static/docs/intro).


## Installation

Just install the **AMWD.Net.Api.FakeFilter** package to your solution.

DotNet CLI
```
dotnet add package AMWD.Net.Api.FakeFilter
```

csproj file with SDK style
```
<ItemGroup>
  <PackageReference Include="AMWD.Net.Api.FakeFilter" Version="x.y.z" />
</ItemGroup>
```


## Usage

#### Check whether the API is available

```cs
using var api = new FakeFilterApi();

if (await api.IsAvailable())
{
  Console.WriteLine("API is available");
}
else
{
  Console.Error.WriteLine("API is not available");
}
```


#### Check whether an e-mail address is used for fake

```cs
string fakeEmail = "something@10minmail.de";
string okEmail = "test@fakefilter.net";

using var api = new FakeFilterApi();
if (await api.IsAvailable())
{
  var fakeResult = await api.IsFakeEmail(fakeEmail);
  var okResult = await api.IsFakeEmail(okEmail);

  if (fakeResult.IsSuccess && fakeResult.IsFakeDomain)
    Console.WriteLine($"The e-mail '{fakeResult.Request}' should be blocked");

  if (okResult.IsSuccess && !okResult.IsFakeDomain)
    Console.WriteLine($"The e-mail '{okResult.Request}' can be processed");
}
else
{
  Console.Error.WriteLine("API not available");
}
```


#### Check whether a domain is used for fake

```cs
string fakeDomain = "10minmail.de";
string okDomain = "fakefilter.net";

using var api = new FakeFilterApi();
if (await api.IsAvailable())
{
  var fakeResult = await api.IsFakeDomain(fakeDomain);
  var okResult = await api.IsFakeDomain(okDomain);

  if (fakeResult.IsSuccess && fakeResult.IsFakeDomain)
    Console.WriteLine($"The domain '{fakeResult.Request}' should be blocked");

  if (okResult.IsSuccess && !okResult.IsFakeDomain)
    Console.WriteLine($"The domain '{okResult.Request}' can be processed");
}
else
{
  Console.Error.WriteLine("API not available");
}
```

### The response model

```
FakeFilterResponse : object
{
  IsSuccess : bool;
  ErrorMessage: string;
  Request: string;
  IsFakeDomain: bool;
  Details: object
  {
    Providers: string[];
    Hosts: dictionary
    {
      Key: string,
      Value: object
      {
        Host: string;
        FirstSeen: datetime;
        LastSeen: datetime;
      }
    }
  }
}
```


## Sources/Docs

- [FakeFilter Docs](https://fakefilter.net/static/docs/restful/)
- [GitHub Repository](https://github.com/7c/fakefilter)
- [MIT License](https://licenses.nuget.org/MIT)
