# NetRube.Data PetaPoco 扩展
PetaPoco extensions

基本用法请参照 http://www.toptensoftware.com/petapoco/

*需要调用 NetRube 基础库 https://github.com/NetRube/NetRube

### 查询
获取记录：

    var a = db.Get<article>().FirstOrDefault();  // SELECT TOP(1) * FROM article
    var a2 = db.Get<article>().Where(e => e.article_id > 3 && e.title.Contains("NetRube")).FirstOrDefault();
    //var a3 = db.Get<article>().Where(e => e.article_id > 3).Where(e => e.title.Contains("NetRube")).FirstOrDefault();
    var a4 = db.Get<article>().Where(e => e.article_id > 3 || e.title.Contains("NetRube")).FirstOrDefault();
    //var a5 = db.Get<article>().Where(e => e.article_id > 3).WhereOr(e => e.title.Contains("NetRube")).FirstOrDefault();

Where 中 string 可以使用以下几个方法：
Contains("NetRube") --> LIKE '%NetRube%'
StartsWith("NetRube") --> LIKE 'NetRube%'
EndsWith("NetRube") --> LIKE '%NetRube'

获取指定字段：

    var a = db.Get<article>().Select(e => e.article_id, e => e.title).FirstOrDefault();
    // SELECT TOP(1) article_id, title FROM article

集合：

    var ls = db.Get<article>().ToList();
    var ls2 = db.Get<article>().Where(e => e.article_id > 3 && e.title.Contains("NetRube")).ToList();

排序：

    var ls = db.Get<article>().OrderByDescending(e => e.date_created).ToList();
    var ls2 = db.Get<article>().OrderByDescending(e => e.date_created, e => e.article_id).ToList();
    var ls3 = db.Get<article>().OrderByDescending(e => e.date_created).OrderBy(e => e.article_id).ToList();

指定范围：

    var ls = db.Get<article>().Take(10).Distinct().ToList();  // SELECT DISTINCT TOP(10) * FROM article
    var ls2 = db.Get<article>().Where(e => e.article_id > 3).Skip(5).Take(10).ToList();
    
分页：

    var pageIndex = 1, pageSize = 20;
    var ls = db.Get<article>().Where(e => e.article_id > 3);
    ls.Where(e => e.title.StartsWith("NetRube"));
    ls.OrderByDescending(e => e.date_created);
    var result = ls.ToPagedList(pageIndex, pageSize);
    
联合查询：

    var ls = db.Get<article>().LeftJoin<author>((e, o) => e.author_id == o.id).Where<author>(o => o.name == "NetRube").ToList();
    // 可以用 InnerJoin、LeftJoin、RightJoin

统计：

    long count = db.Get<article>().Count<long>();
    int count2 = db.Get<article>().Count();
    var count3 = db.Get<article>(e => e.article_id > 3).Count();
    var count4 = db.Get<article>().Where(e => e.article_id > 3).Count();
    long sum = db.Get<article>().Sum<long>(e => e.xx);
    int sum2 = db.Get<article>().Sum(e => e.xx);
    // Max()、Min() 用法一样

Exist：

    var e = db.Get<article>().Where(e => e.article_id == 3).Exist();
    
### 插入

    var a = new article();
    a.title = "My new article";
    a.content = "PetaPoco was here";
    a.date_created = DateTime.UtcNow;
    bool s = db.Add<article>(a); // 是否插入成功
    long id = db.Add<long>(a);  // 返回插入成功后的 id 号
    
### 更新

    var n = db.Set<article>().Set(e => e.title, "NetRube").Where(e => e.article_id == 1).Execute(); // 返回受影响记录数
    // UPDATE article SET title = "NetRube" WHERE article_id = 1
    var s = db.Set<article>().Set(e => e.hits, e.hits + 1).Where(e => e.article_id == 1).Succeed(); // 是否更新成功
    // UPDATE article SET hits = hits + 1 WHERE article_id = 1
    
按需更新

    var a = db.Get<article>().Where(e => e.article_id == 1).FirstOrDefault();
    var ss = NetRube.TrackingEntity<article>.Start(a); // 跟踪实体变化
    a.title = "NetRube"; // 如果原实体 title 不等于 "NetRube" 将会更新此字段，如果跟原值一样将不会更新，没有重新赋值的字段也将不会更新
    db.Set<article>(ss).Where(e => e.article_id == 1).Execute();

### 删除

    var n = db.Del<article>().Where(e => e.article_id == 1).Execute(); // 返回受影响记录数
    var s = db.Del<article>().Where(e => e.article_id == 1).Succeed(); // 是否删除成功
    
### 事务

    var s = db.InTransaction(() => {
	    db.Add...
	    db.Set...
    });

    var s2 = db.InTransaction(() => {
	    db.Add...
	    var s = db.Set...
	    if (s)
		    return true;
		return false;
    }); // 返回 false 时会导致整个嵌套的事务全部回滚

### 高级用法

一对多、多对一、多对多映射：
Map() 映射方法参考：http://www.toptensoftware.com/Articles/115/PetaPoco-Mapping-One-to-Many-and-Many-to-One-Relationships
    
    var authors = new Dictionary<long, author>();
    var posts = db.Get<post>()
	.LeftJoin<author>((e, o) => e.author == o.id)
	.Map<post, author>((p, a) =>
    	{
		// Get existing author object
		author aExisting;
		if (authors.TryGetValue(a.id, out aExisting))
			a = aExisting;
		else
			authors.Add(a.id, a);
		// Wire up objects
		p.author_obj = a;
		return p;
    	})
    	.OrderBy(e => e.id)
    	.ToList();
    	

    author current;
    var ls = db.Get<author>()
    	.LeftJoin<post>((a, p) => a.id == p.author)
    	.Map<author, post>((a, p) =>
    	{
    		if (a == null) return current;
    		if (current != null && current.id == a.id)
    		{
    			current.posts.Add(p);
    			return null;
    		}
    		var prev = current;
    		current = a;
    		current.posts = new List<post>();
    		current.posts.Add(p);
    		return prev;
    	})
    	.OrderBy(e => e.id)
    	.ToList();
    	
 In_查询：
 
     var ids = new List<int>() { 1, 2, 3, 4 };
     var ls = db.Get<author>().Where(e => e.id.In_(ids)).ToList();

     var pids = db.Get<post>().Select(e => e.id).Where(e => e.id < 10); // 最后不要调用 ToList()
     var ls = db.Get<author>().Where(e => e.id.In_(pids)).ToList(); // In_() 里将调用 pids 生成的 SQL 语句




在上面所有的例子中 db.Get/Set/Add/Del 到最后的执行方法（FirstOrDefault()、ToList()、Count()、Execute()等）之间的 Where、OrderBy、LeftJoin、Set、Select等等都可以以任意次序和次数调用，如：

    var ls = db.Get<article>()
    		.OrderBy(e => e.date_created)
    		.Where(e => e.article_id > 3)
    		.Select(e => e.article_id)
    		.OrderByDescending(e => e.article_id)
    		.Where(e => e.title.Contains("NetRube"))
    		.Select(e => e.title)
    		.ToList(); // 最后调用执行方法

整条语句也可以任意分成几段，同理，只要在最后调用执行方法就可以了：

    var ls = db.Get<article>().OrderBy(e => e.date_created);
    ls.Where(e => e.article_id > 3).Select(e => e.article_id);
    var title = Request("title");
    if(!string.IsNullOrEmpty(title))
    	ls.Where(e => e.title.Contains(title));
    ls.OrderByDescending(e => e.article_id)
    	.Select(e => e.title);
    var result = ls.ToList(); // 最后调用执行方法
    	
