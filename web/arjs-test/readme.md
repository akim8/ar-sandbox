# Notes

The image for the custom marker has to be **simple** and a black image on grey background for best results.

If the camera can see more than one of the same marker, it will cause problems with tracking.

To load resources like models, the website has to be hosted on a web server to prevent CORS errors.

WebRTC getUserMedia is currently not supported in iOS's WKWebView. (iOS 13.1.3) What this means is that any non-Safari browser (in-app Safari browsers, Chrome) or browser technology (Ionic, React) cannot use this api. Therefore, AR.js only works in the Safari app.
