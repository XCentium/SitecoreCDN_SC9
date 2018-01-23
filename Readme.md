# Sitecore CDN
*Requirements:*
* .Net 4.6.2
* Visual Studio 2015/2017
* A project configured with Sitecore 9.X website.

## How does it work?
There are two Cloud front instances that have been configured as origin.  
This means that those instances will use one of our SiteCore front end 
instances as the source for content when requests are made for CDN.  The process is as follows:
1.	 User requests a page on a site running with CDN.
2.	All content on that page that is static (images, JavaScript, cshtml)
	is updated to have the CDN hostname rather than the original server 
	hostname (acc.org).
3.	When the page renders on the client browser, the client browser makes 
	requests to the CDN for the static content.
4.	Once contacted, the CDN will either serve up the content if it has 
	it in cache or it will make a request to the origin server for that 
	content.  Once the request is made the content will be available via 
	the CDN rather than the origin.

 
## Configuration
In your include folder is a file named SitecoreCDN.config.  The most 
important section is highlighted below:
```xml
<cdn>
    <excludeRequests>
        <!--<regex pattern = "Default\.aspx" />-->
        <regex pattern = "\.woff|\.ttf" />

    </excludeRequests>

    <!-- These regex patterns will prevent matching urls from being replaced in 
	the outgoing html, doesn't affect Incoming request processing -->
    <excludeUrls>
        <regex pattern = "\.axd|\.asmx|\.woff|\.ttf|\{\{.*\}\}" />
        <!-- 
        <regex pattern = "\.asmx" />
        <regex pattern = "\.woff" />
        <regex pattern = "\.ttf" /> -->
        <!-- this keeps ScriptResource.axd and WebResource.axd from being CDN'd-->
        <regex pattern = "VisitorIdentification.aspx" />
        <regex pattern = "fonts\.css" />
        <!-- this keeps the Sitecore Analytics request from being CDN'd -->        
    </excludeUrls>
</cdn>
```

Enable and matchProtocol set to true will turn on CDN functionality.  
The CDN has been configured to use https for all CDN content to avoid 
security errors when running in https.  

To disable CDN only requires setting enable to false.
 
For all pages across the site to function properly there are some urls 
that cannot be modified.  This includes handlers, webservice methods, 
fonts and all Angular  directives.  The exclude patterns are .net 
regular expressions.  Additional expressions can be added by preceding 
them with a pipe character (“|”) .  Too many exclude parameters can 
slow performance of the CDN.

 
This is the hostname of the server hat was created in Amazon Cloudfront.  
This is provided in the Amazon console.  Stage and production environments 
have different hostnames since the content is different in the environments.  
The hostname for the stage environment is d1pnubyx8h8qiv.cloudfront.net.
