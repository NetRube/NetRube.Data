# NetRube.Data PetaPoco 扩展
PetaPoco extensions

基本用法请参照 http://www.toptensoftware.com/petapoco/

### 查询
统计：

    long count = db.Get<article>().Count<long>();
    int count2 = db.Get<article>().Count();
    var count3 = db.Get<article>(e => e.article_id > 3).Count();
    var count4 = db.Get<article>().Where(e => e.article_id > 3).Count();

集合：

    var ls = db.Get<article>().ToList();
    var ls2 = db.Get<article>().Where(e => e.article_id > 3 && e.title.Contains("Net")).ToList();
分页：

    var pageIndex = 1, pageSize = 20;
    var ls = db.Get<article>().Where(e => e.article_id > 3);
    ls.Where(e => e.title.StartsWith("Net"));
    ls.OrderByDescending(e => e.date_created);
    var result = ls.ToPagedList(pageIndex, pageSize);
### 插入

    var a = new article();
    a.title = "My new article";
    a.content = "PetaPoco was here";
    a.date_created = DateTime.UtcNow;
    bool s = db.Add<article>(a); // 是否插入成功
    long id = db.Add<long>(a);  // 返回插入成功后的 id 号
### 更新

    var s = db.Set<article>().Set(e => e.title, e.title + " Net Rube").Where(e => e.article_id == 1).Succeed(); // 是否更新成功
按需更新

    var a = db.Get<article>().Where(e => e.article_id == 1).FirstOrDefault();
    var ss = NetRube.TrackingEntity<article>.Start(a);
    a.title = "Net Rube"; // 如果原实体 title 不等于 "Net Rube" 将会更新此字段，如果跟原值一样将不会更新，没有重新赋值的字段也将不会更新
    db.Set<article>().Where(e => e.article_id == 1).Execute();

### 删除

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
    });
