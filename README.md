MVC Output Cache
-

MVC Output Cache is an attempt to implement output caching for ASP.net MVC that fixes some of the limitations of the OutputCache attribute that is part of ASP.net. The idea is to make easy things easy, and hard things possible, to steal a phrase. Some scenarios:

  - Cache only for anonymous users
  - Simply not cache under defined circumstances
  - Invalidate the cache for the current user
  - Vary by ajax (very helpful if you do progressive enhancement)
  - Use a different cache duration for anonymous and authenticated users

It's made to work in conventional cases out of the box, but the core methods are **designed to be overridden** in inherited classes. For example, you might override the IsAnonymous method based on your own auth system, or have your own ideas about what "invalidate" means.