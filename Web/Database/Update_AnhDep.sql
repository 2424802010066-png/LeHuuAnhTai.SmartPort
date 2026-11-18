/* Cap nhat bo anh dep hon — CHI de khi URL van la anh mac dinh cu
   (khong dong vao anh admin da tu doi). Chay ca local + hosting. */
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop' WHERE Khoa='g3'   AND Url LIKE '%1494412574643%';
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1580674285054-bed31e145f59?q=80&w=1200&auto=format&fit=crop' WHERE Khoa='g6'   AND Url LIKE '%1519452635265%';
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1605732562742-3023a888e56e?q=80&w=1800&auto=format&fit=crop' WHERE Khoa='slide4' AND Url LIKE '%1493946740644%';
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1605732562742-3023a888e56e?q=80&w=1200&auto=format&fit=crop' WHERE Khoa='gal1' AND Url LIKE '%1494412574643%';
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop' WHERE Khoa='gal6' AND Url LIKE '%1605745341112%';
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1473445361085-b9a07f55608b?q=80&w=800&auto=format&fit=crop'  WHERE Khoa='biz1' AND Url LIKE '%1600880292203%';
UPDATE CauHinhAnh SET Url='https://images.unsplash.com/photo-1578574577315-3fbeb0cecdc2?q=80&w=800&auto=format&fit=crop'  WHERE Khoa='biz2' AND Url LIKE '%1521791136064%';
SELECT Khoa, LEFT(Url, 60) AS Url60 FROM CauHinhAnh WHERE Khoa IN ('g3','g6','slide4','gal1','gal6','biz1','biz2');
