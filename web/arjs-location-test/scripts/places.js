window.onload = () => {
    var placesUrl = 'https://gist.githubusercontent.com/akim8/386cb2d8ad833adac5d23459301454ff/raw/8edb56f2004aa71c5f82381a2b1901d2e90008dc/uofm-bus-stop-coordinates.json';

    navigator.geolocation.getCurrentPosition(function (position) {

        // add user coords to corner display
        document.getElementById('user-coords-lat').innerHTML = position.coords.latitude;
        document.getElementById('user-coords-lon').innerHTML = position.coords.longitude;
        document.getElementById('user-coords-acc').innerHTML = position.coords.accuracy;
        document.getElementById('user-location-link').href = "https://www.google.com/maps/search/?api=1&query=" + position.coords.latitude + "," + position.coords.longitude;

        // get places data
        getPlaces(placesUrl, parsePlaces, position.coords)
    });
};

// https://developer.mozilla.org/en-US/docs/Web/API/XMLHttpRequest/Synchronous_and_Asynchronous_Requests
function getPlaces(url, callback, userCoords) {
    var xhr = new XMLHttpRequest();
    xhr.callback = callback;
    xhr.arguments = Array.prototype.slice.call(arguments, 2);
    xhr.onload = xhrSuccess;
    xhr.onerror = xhrError;
    xhr.open("GET", url, true);
    xhr.send(null);
}
function xhrSuccess() { this.callback.apply(this, this.arguments); }
function xhrError() { console.error(this.statusText); }

function parsePlaces(userCoords) {

    var maxDistance = 1; // miles
    var scaleFactor = 20; // size of dots

    const scene = document.querySelector('a-scene');
    var places = JSON.parse(this.responseText);

    places.forEach(function (element) {
        var distance = distanceBetweenCoords(element.location.lat, element.location.lon, userCoords.latitude, userCoords.longitude);
        console.log("Distance from user to " + element.name + ": " + distance.toFixed(1) + "mi");
        document.getElementById('user-coords-display-text').innerHTML += "<br>Distance from user to " + element.name + ": " + distance.toFixed(5) + " mi";

        if (distance <= maxDistance) {
            const latitude = element.location.lat;
            const longitude = element.location.lon;
            var scale = scaleFactor - distance * maxDistance * 100;

            // create place in AR

            const node = document.createElement('a-image');
            node.setAttribute('gps-entity-place', `latitude: ${element.location.lat}; longitude: ${element.location.lon};`);
            node.setAttribute('scale', `${scaleFactor} ${scaleFactor}`);
            node.setAttribute('name', element.name);
            node.setAttribute('src', '../images/bus-stop-sign.png');
    
            scene.appendChild(node);

            console.log("Created " + element.name + " node!");
            document.getElementById('user-coords-display-text').innerHTML += "<br>Created " + element.name + " node!";
        }
    });
}

// https://stackoverflow.com/a/365853
function distanceBetweenCoords(lat1, lon1, lat2, lon2) {
    var earthRadiusMi = 3958.8;

    var dLat = degreesToRadians(lat2 - lat1);
    var dLon = degreesToRadians(lon2 - lon1);

    lat1 = degreesToRadians(lat1);
    lat2 = degreesToRadians(lat2);

    var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
        Math.sin(dLon / 2) * Math.sin(dLon / 2) * Math.cos(lat1) * Math.cos(lat2);
    var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return earthRadiusMi * c;
}

function degreesToRadians(degrees) {
    return degrees * Math.PI / 180;
}
