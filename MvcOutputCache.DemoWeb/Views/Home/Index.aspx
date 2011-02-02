<%@ Page Language="C#" Inherits="System.Web.Mvc.ViewPage" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
	<title>Mvc Output Cache</title>
</head>
<body>
	<div>
	<h1>Mvc Output Cache quickie demo</h1>
	<p>
	The HTML of this page was cached at <%= DateTime.UtcNow.ToString("R") %>
	</p>
	<p>
	JavaScript says the current time is
	<script type="text/javascript">
		document.write((new Date()).toUTCString());
	</script>
	</p>

	<p><a href="">Reload</a></p>
	</div>
</body>
</html>
