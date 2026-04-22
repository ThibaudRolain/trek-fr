using System;
using System.Collections.Generic;

namespace TrekFr.Api.Tracks;

public sealed record TrackWeatherRequest(
    IReadOnlyList<WeatherPointInput> Points,
    DateOnly? StartDate = null,
    int Days = 7);

public sealed record WeatherPointInput(
    string Label,
    double Latitude,
    double Longitude);
